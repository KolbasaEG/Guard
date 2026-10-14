using System.Buffers.Binary;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Guard.Core.Services.Exports;

public sealed class ExportStorage(IDataProtectionProvider protection, IOptions<ExportOptions> options, IWebHostEnvironment environment)
{
  public SemaphoreSlim ArchiveGate { get; } = new(1, 1);
  private string? rootPath;
  private readonly object rootGate = new();
  private string Root
  {
    get {
      lock (rootGate) {
      if (rootPath != null) return rootPath;
      var root = Path.GetFullPath(options.Value.Directory, environment.ContentRootPath);
      var web = Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
      if (root.Equals(web, StringComparison.OrdinalIgnoreCase) || root.StartsWith(web + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Архив нельзя размещать в wwwroot.");
      SecureDirectory(root); return rootPath = root;
      }
    }
  }
  public string WorkDirectory(Guid id) { var dir = Path.Combine(Root, "work", id.ToString("N")); SecureDirectory(dir); return dir; }
  public string ArtifactPath(string key)
  {
    if (!Guid.TryParseExact(key, "N", out _)) throw new ArgumentException("Некорректный ключ хранилища.");
    return Path.Combine(Root, key + ".protected");
  }
  public void CheckSpace()
  {
    var root = Root; var drive = new DriveInfo(Path.GetPathRoot(root)!);
    if (drive.AvailableFreeSpace < options.Value.MinimumFreeBytes) throw new IOException("Недостаточно места для выгрузки.");
  }
  public void DeleteWork(Guid id) { var path = WorkDirectory(id); Directory.Delete(path, true); }
  public void Delete(string key) { File.Delete(ArtifactPath(key)); }
  public IEnumerable<string> Orphans(DateTimeOffset before) => Directory.EnumerateFiles(Root, "*.protected")
    .Where(p => File.GetLastWriteTimeUtc(p) < before.UtcDateTime).Select(p => Path.GetFileNameWithoutExtension(p));
  public IEnumerable<Guid> WorkIds() => Directory.Exists(Path.Combine(Root, "work")) ?
    Directory.EnumerateDirectories(Path.Combine(Root, "work")).Select(Path.GetFileName).Where(n => Guid.TryParseExact(n, "N", out _)).Select(n => Guid.ParseExact(n!, "N")) : [];
  private IDataProtector Protector(string purpose, long index) => protection.CreateProtector("Guard.Exports.v1", purpose, index.ToString(System.Globalization.CultureInfo.InvariantCulture));
  private static void SecureDirectory(string path)
  {
    if (OperatingSystem.IsWindows()) SecureWindows(path);
    else Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
  }
  [SupportedOSPlatform("windows")]
  private static void SecureWindows(string path)
  {
    var directory = Directory.CreateDirectory(path);
    var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false);
    var user = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("Не определена учётная запись сервера.");
    acl.SetOwner(user);
    foreach (var sid in new[] { user, new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
      acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
    directory.SetAccessControl(acl);
  }
  public async Task<(string Key, string Hash, long Size)> SaveAsync(string path, CancellationToken ct)
  {
    CheckSpace(); var key = Guid.NewGuid().ToString("N"); var target = ArtifactPath(key);
    try {
      await using var input = File.OpenRead(path);
      await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true);
      using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
      var buffer = new byte[65536]; long index = 0, size = 0; int count;
      while ((count = await input.ReadAsync(buffer, ct)) > 0) {
        CheckSpace(); hash.AppendData(buffer, 0, count); size += count;
        await WriteFrameAsync(output, key, index++, buffer.AsMemory(0, count), ct);
      }
      await WriteFrameAsync(output, key, index, ReadOnlyMemory<byte>.Empty, ct);
      await output.FlushAsync(ct);
      return (key, Convert.ToHexString(hash.GetHashAndReset()), size);
    }
    catch { File.Delete(target); throw; }
  }
  public Stream Open(string key) => new ProtectedInput(File.OpenRead(ArtifactPath(key)), this, key);
  public async Task VerifyAsync(string key, string expectedHash, long expectedSize, CancellationToken ct)
  {
    await using var stream = Open(key); using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    var buffer = new byte[65536]; long size = 0; int count;
    while ((count = await stream.ReadAsync(buffer, ct)) > 0) { hash.AppendData(buffer, 0, count); size += count; }
    if (size != expectedSize || Convert.ToHexString(hash.GetHashAndReset()) != expectedHash)
      throw new CryptographicException("Контрольная сумма архивной копии не совпадает.");
  }
  internal async Task WriteFrameAsync(Stream stream, string purpose, long index, ReadOnlyMemory<byte> data, CancellationToken ct)
  {
    var encrypted = Protector(purpose, index).Protect(data.ToArray());
    var length = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(length, encrypted.Length);
    await stream.WriteAsync(length, ct); await stream.WriteAsync(encrypted, ct);
  }
  internal async Task<byte[]> ReadFrameAsync(Stream stream, string purpose, long index, CancellationToken ct)
  {
    var length = new byte[4]; await stream.ReadExactlyAsync(length, ct);
    var size = BinaryPrimitives.ReadInt32LittleEndian(length);
    if (size is < 1 or > 4 * 1024 * 1024) throw new CryptographicException("Некорректная структура архива.");
    var encrypted = new byte[size]; await stream.ReadExactlyAsync(encrypted, ct);
    return Protector(purpose, index).Unprotect(encrypted);
  }
  public async IAsyncEnumerable<ExportRow> RowsAsync(Guid id, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
  {
    await using var stream = File.OpenRead(Path.Combine(WorkDirectory(id), "rows.protected")); long index = 0;
    while (true) {
      var bytes = await ReadFrameAsync(stream, "rows:" + id, index++, ct);
      if (bytes.Length == 0) { if (stream.Position != stream.Length) throw new CryptographicException("Лишние данные в снимке."); yield break; }
      yield return JsonSerializer.Deserialize<ExportRow>(bytes) ?? throw new InvalidDataException("Некорректная строка снимка.");
    }
  }
  private sealed class ProtectedInput(Stream input, ExportStorage owner, string purpose) : Stream
  {
    private byte[] buffer = []; private int offset; private long index; private bool ended;
    public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken ct = default)
    {
      if (destination.Length == 0 || ended) return 0;
      if (offset == buffer.Length) {
        buffer = await owner.ReadFrameAsync(input, purpose, index++, ct); offset = 0;
        if (buffer.Length == 0) { if (input.Position != input.Length) throw new CryptographicException("Лишние данные в архиве."); ended = true; return 0; }
      }
      var count = Math.Min(destination.Length, buffer.Length - offset); buffer.AsMemory(offset, count).CopyTo(destination); offset += count; return count;
    }
    public override int Read(byte[] target, int start, int count) => ReadAsync(target.AsMemory(start, count)).AsTask().GetAwaiter().GetResult();
    protected override void Dispose(bool disposing) { if (disposing) input.Dispose(); base.Dispose(disposing); }
    public override async ValueTask DisposeAsync() { await input.DisposeAsync(); GC.SuppressFinalize(this); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
  }
}
