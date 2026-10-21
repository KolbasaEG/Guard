using Guard.Core.Services.DTOs;
using Serilog.Core;
using Serilog.Events;
using System.Text.Json;

namespace Guard.Core.Logging;

/// <summary>Один владелец на каталог. Подтверждение БД предшествует удалению; потеря ACK вызывает безопасный повтор.</summary>
public sealed class TechnicalLogBuffer : ILogEventSink, IDisposable
{
  private readonly object gate = new();
  private readonly TechnicalLogRuntime runtime;
  private readonly Func<long> freeSpace;
  private readonly Queue<DateTime> errors = new();
  private readonly SortedSet<string> pendingPaths = new(StringComparer.Ordinal);
  private FileStream? lease;
  private bool disposed;
  private long pending, bytes, lost;
  private long quarantined, quarantinedBytes;
  private string? bufferError, databaseError, cleanupError;
  private DateTime? lastLocal, lastDatabase, lastProbe, lastWorker, lastCleanup;
  private DateTime? lastLocalProbe, failureSince, lastRecovery;
  private long? free;
  public string DirectoryPath { get; }
  public const int MaximumEventBytes = 64 * 1024;
  public const int MaximumFiles = 3000000;

  public TechnicalLogBuffer(TechnicalLogRuntime runtime, string directory = "logs/technical-buffer", Func<long>? freeSpace = null)
  {
    this.runtime = runtime; DirectoryPath = Path.GetFullPath(directory);
    this.freeSpace = freeSpace ?? (() => new DriveInfo(Path.GetPathRoot(DirectoryPath)!).AvailableFreeSpace);
    try {
      EnsureOwner();
      foreach (var file in Directory.EnumerateFiles(DirectoryPath, "*.tmp")) File.Delete(file);
      RefreshCounts();
      var counter = Path.Combine(DirectoryPath, ".lost");
      if (File.Exists(counter) && long.TryParse(File.ReadAllText(counter), out var count)) lost = Math.Max(0, count);
    } catch (Exception error) when (Recoverable(error)) { bufferError = error.GetType().Name; }
  }
  public void Emit(LogEvent entry)
  {
    try {
      if (entry.Properties.TryGetValue("TechnicalLogInternal", out var internalValue) && internalValue is ScalarValue { Value: true }) return;
      if (!runtime.Accept(entry)) return;
      var record = TechnicalLogRecord.Create(entry, runtime);
      lock (gate) {
        if (disposed) return;
        if (entry.Level >= LogEventLevel.Error) { Prune(); if (errors.Count < 100000) errors.Enqueue(runtime.UtcNow); }
        var data = JsonSerializer.SerializeToUtf8Bytes(record);
        var settings = runtime.Read().Settings;
        free = freeSpace();
        if (lease == null) throw new IOException("BufferOwnerUnavailable");
        if (data.Length > MaximumEventBytes) { Lose("EventTooLarge"); return; }
        if (pending + quarantined >= MaximumFiles || bytes + quarantinedBytes + data.Length > settings.BufferMegabytes * 1024L * 1024) { Lose("BufferLimit"); return; }
        if (free - data.Length < settings.MinimumFreeMegabytes * 1024L * 1024) { Lose("DiskReserve"); return; }
        var path = Path.Combine(DirectoryPath, $"{runtime.UtcNow.Ticks:D19}-{record.Id:N}.json");
        try {
          using (var stream = new FileStream(path + ".tmp", FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
            stream.Write(data); stream.Flush(flushToDisk: true);
          }
          File.Move(path + ".tmp", path);
        } finally { if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp"); }
        pendingPaths.Add(path); pending++; bytes += data.Length; lastLocal = runtime.UtcNow; bufferError = null;
      }
    } catch (Exception error) when (Recoverable(error)) { lock (gate) Lose(error.GetType().Name); }
  }
  private void Lose(string code)
  {
    lost++; bufferError = code;
    try {
      if (lease != null) {
        var counter = Path.Combine(DirectoryPath, ".lost");
        using (var stream = new FileStream(counter + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None)) {
          var data = System.Text.Encoding.UTF8.GetBytes(lost.ToString(System.Globalization.CultureInfo.InvariantCulture));
          stream.Write(data); stream.Flush(true);
        }
        File.Move(counter + ".tmp", counter, true);
      }
    }
    catch (Exception error) when (Recoverable(error)) { }
  }
  public IReadOnlyList<(string Path, TechnicalLogRecord Record)> ReadBatch(int maximum = 100)
  {
    lock (gate) {
      if (lease == null) throw new IOException("BufferOwnerUnavailable");
      var result = new List<(string, TechnicalLogRecord)>();
      foreach (var path in pendingPaths.Take(1000).ToArray()) {
        try {
          var record = ReadRecord(path);
          result.Add((path, record));
          if (result.Count >= maximum) break;
        } catch (Exception error) when (error is JsonException or InvalidDataException) {
          var length = new FileInfo(path).Length;
          File.Move(path, path + ".bad"); pendingPaths.Remove(path); pending--; bytes -= length; quarantined++; quarantinedBytes += length;
          Lose("CorruptEventQuarantined");
        } catch (Exception error) when (Recoverable(error)) { bufferError = "Read:" + error.GetType().Name; }
      }
      return result;
    }
  }
  private static TechnicalLogRecord ReadRecord(string path)
  {
    if (new FileInfo(path).Length > MaximumEventBytes) throw new InvalidDataException("BufferEventTooLarge");
    var record = JsonSerializer.Deserialize<TechnicalLogRecord>(File.ReadAllBytes(path)) ?? throw new InvalidDataException();
    if (record.Id == Guid.Empty || record.Timestamp.Kind != DateTimeKind.Utc ||
      !Enum.TryParse<LogEventLevel>(record.Level, out var level) || !Enum.IsDefined(level) ||
      record.Properties == null || record.Message == null || record.MessageTemplate == null ||
      record.Layer == null || record.Layer.Length > 50 || record.UserId?.Length > 250 || record.ClientIp?.Length > 50 ||
      new[] { record.Message, record.MessageTemplate, record.Exception, record.UserId, record.ClientIp, record.Layer,
        record.CorrelationId, record.SourceContext, record.RunId, record.Fingerprint, record.ApplicationVersion, record.InstanceId }.Any(s => s?.Contains('\0') == true))
      throw new InvalidDataException("InvalidBufferEvent");
    using var properties = JsonDocument.Parse(record.Properties, new JsonDocumentOptions { MaxDepth = 16 });
    if (properties.RootElement.ValueKind != JsonValueKind.Object || ContainsNul(properties.RootElement)) throw new InvalidDataException("InvalidBufferProperties");
    return record;
  }
  // Чтение не выполняет ACK, карантин и очистку. Каждая запись блокирует доставку только на время чтения файла.
  public (IReadOnlyList<(string Cursor, TechnicalLogRecord Record)> Records, bool Truncated, int Scanned, int Unreadable, string? NextCursor) Snapshot(
    TechnicalLogExportRequest request, CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();
    string[] paths;
    lock (gate) {
      if (disposed || lease == null) throw new IOException("BufferOwnerUnavailable");
      IEnumerable<string> candidates = pendingPaths;
      if (request.AfterCursor != null && pendingPaths.Count > 0) {
        var lower = Path.Combine(DirectoryPath, request.AfterCursor + ".json");
        candidates = StringComparer.Ordinal.Compare(lower, pendingPaths.Max) >= 0 ? [] :
          pendingPaths.GetViewBetween(lower, pendingPaths.Max!).SkipWhile(path => path == lower);
      }
      paths = candidates.Take(1001).ToArray();
    }
    var result = new List<(string, TechnicalLogRecord)>(); var scanned = 0; var unreadable = 0; long readBytes = 0;
    var truncated = paths.Length > 1000;
    string? cursor = null;
    foreach (var path in paths.Take(1000)) {
      ct.ThrowIfCancellationRequested();
      lock (gate) {
        if (!pendingPaths.Contains(path)) continue; // Доставка уже подтвердила и удалила событие.
        try {
          var length = new FileInfo(path).Length;
          if (readBytes + length > 8 * 1024 * 1024) { truncated = true; break; }
          readBytes += length; scanned++; cursor = Path.GetFileNameWithoutExtension(path);
          var record = ReadRecord(path);
          if (record.Timestamp >= request.FromUtc && record.Timestamp < request.ToUtc &&
            (string.IsNullOrWhiteSpace(request.CorrelationId) || record.CorrelationId == request.CorrelationId) &&
            (request.DiagnosticSessionId == null || record.DiagnosticSessionId == request.DiagnosticSessionId) &&
            (request.Level == null || record.Level == request.Level)) result.Add((cursor!, record));
        } catch (Exception error) when (Recoverable(error)) { unreadable++; cursor = Path.GetFileNameWithoutExtension(path); }
      }
    }
    return (result, truncated, scanned, unreadable, truncated ? cursor : null);
  }
  private static bool ContainsNul(JsonElement element) => element.ValueKind switch {
    JsonValueKind.String => element.GetString()?.Contains('\0') == true,
    JsonValueKind.Object => element.EnumerateObject().Any(p => p.Name.Contains('\0') || ContainsNul(p.Value)),
    JsonValueKind.Array => element.EnumerateArray().Any(ContainsNul), _ => false
  };
  public void Acknowledge(IReadOnlyList<(string Path, TechnicalLogRecord Record)> batch)
  {
    lock (gate) {
      lastDatabase = runtime.UtcNow; databaseError = null;
      foreach (var item in batch) {
        if (!pendingPaths.Contains(item.Path)) continue;
        var length = new FileInfo(item.Path).Length; File.Delete(item.Path); pendingPaths.Remove(item.Path); pending--; bytes -= length;
      }
    }
  }
  public void Worker(string? error = null) { lock (gate) { lastWorker = runtime.UtcNow; databaseError = error; } }
  public void DatabaseProbe() { lock (gate) { lastProbe = runtime.UtcNow; databaseError = null; } }
  public void Cleanup(string? error) { lock (gate) { lastCleanup = runtime.UtcNow; cleanupError = error; } }
  public void ProbeLocal()
  {
    lock (gate) {
      try {
        EnsureOwner();
        free = freeSpace();
        var settings = runtime.Read().Settings;
        if (free < settings.MinimumFreeMegabytes * 1024L * 1024) { bufferError = "DiskReserve"; return; }
        var probe = Path.Combine(DirectoryPath, ".probe");
        using (var stream = new FileStream(probe, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.WriteByte(1); stream.Flush(true); }
        File.Delete(probe);
        lastLocalProbe = runtime.UtcNow;
        if (bytes + quarantinedBytes >= settings.BufferMegabytes * 1024L * 1024 || pending + quarantined >= MaximumFiles) bufferError = "BufferLimit";
        else bufferError = null;
      } catch (Exception error) when (Recoverable(error)) { bufferError = error.GetType().Name; }
    }
  }
  public TechnicalLogHealth Health()
  {
    lock (gate) {
      Prune();
      DateTime? oldest = null;
      try {
        var first = pendingPaths.Min;
        if (first != null && long.TryParse(Path.GetFileName(first).Split('-')[0], out var ticks)) oldest = new DateTime(ticks, DateTimeKind.Utc);
      } catch (Exception error) when (Recoverable(error)) { bufferError = error.GetType().Name; }
      var problems = new List<string>();
      if (bufferError != null) problems.Add("LocalWrite:" + bufferError);
      if (quarantined > 0) problems.Add("Quarantine");
      if (lastWorker == null || runtime.UtcNow - lastWorker > TimeSpan.FromSeconds(30)) problems.Add("WorkerStale");
      if (lastLocalProbe == null || runtime.UtcNow - lastLocalProbe > TimeSpan.FromSeconds(30)) problems.Add("LocalProbeStale");
      if (databaseError != null) problems.Add("Database:" + databaseError);
      if (lastProbe == null || runtime.UtcNow - lastProbe > TimeSpan.FromSeconds(60)) problems.Add("DatabaseProbeStale");
      if (cleanupError != null) problems.Add("Cleanup:" + cleanupError);
      if (runtime.ConfigurationError != null) problems.Add("Configuration");
      var status = bufferError != null || quarantined > 0 || problems.Contains("WorkerStale") || problems.Contains("LocalProbeStale")
        ? "Emergency" : problems.Count > 0 ? "Degraded" : "Ready";
      if (status != "Ready") failureSince ??= runtime.UtcNow;
      else if (failureSince != null) { lastRecovery = runtime.UtcNow; failureSince = null; }
      return new(runtime.UtcNow, status, bufferError, databaseError, lastLocal, lastDatabase, lastProbe, lastWorker,
        lastCleanup, cleanupError, pending, bytes, oldest, lost, errors.Count,
        errors.Count >= runtime.Read().Settings.ErrorThreshold, free, runtime.ConfigurationError, quarantined, quarantinedBytes,
        problems, failureSince, lastRecovery, lastLocalProbe);
    }
  }
  private void Prune() { var cutoff = runtime.UtcNow.AddMinutes(-runtime.Read().Settings.ErrorWindowMinutes); while (errors.TryPeek(out var time) && time <= cutoff) errors.Dequeue(); }
  private void RefreshCounts() {
    pendingPaths.Clear(); pending = 0; bytes = 0; quarantined = 0; quarantinedBytes = 0;
    foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.json")) { pendingPaths.Add(path); pending++; bytes += new FileInfo(path).Length; }
    foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.bad")) { quarantined++; quarantinedBytes += new FileInfo(path).Length; }
  }
  private void EnsureOwner()
  {
    if (disposed) throw new IOException("BufferDisposed");
    if (lease != null) return;
    Directory.CreateDirectory(DirectoryPath);
    lease = new FileStream(Path.Combine(DirectoryPath, ".owner"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    RefreshCounts();
  }
  private static bool Recoverable(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.SecurityException or JsonException or ArgumentException;
  public void Dispose() { lock (gate) { if (disposed) return; disposed = true; lease?.Dispose(); } }
}
