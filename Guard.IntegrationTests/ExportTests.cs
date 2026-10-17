using Guard.Components.Library;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services.DTOs;
using Guard.Core.Services.Exports;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Radzen;

internal static class ExportTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    foreach (var area in Enum.GetValues<ExportArea>()) {
      check(ExportSchema.Headers(area).All(h => !string.IsNullOrWhiteSpace(h)) && !ExportSchema.Headers(area).Contains("Version"), "export headers use DisplayName and exclude technical fields");
      check(ExportSchema.Properties(area).Length == ExportSchema.Headers(area).Length, "fixed export columns match DTO schema");
    }
    var values = new List<string> { "A" };
    var descriptor = new CompositeFilterDescriptor { Property = "Address", Type = typeof(IEnumerable<string>), FilterOperator = FilterOperator.In, FilterValue = values };
    var captured = ExportFilterCapture.Capture([descriptor], LogicalFilterOperator.And);
    descriptor.FilterValue = new[] { "B" }; values.Clear();
    captured = JsonSerializer.Deserialize<ExportFilter>(JsonSerializer.Serialize(captured))!;
    var ips = new[] { new IpAddress { Address = "A" }, new IpAddress { Address = "B" } }.AsQueryable();
    check(ExportFilters.Apply(ips, captured, ExportQueries.IpFields).Single().Address == "A", "serialized applied filter is independent from UI edits");
    try { DataExportService.Validate(new(ExportArea.IpAddresses, ExportFormat.Excel, Filter: new("Personals.FirstName", "Equals", JsonSerializer.SerializeToElement("secret")))); check(false, "unsupported export field rejected"); }
    catch (ArgumentException) { check(true, "unsupported export field rejected"); }
    var rootId = Guid.NewGuid(); var childId = Guid.NewGuid();
    var root = new ExportSubdivision(rootId, null, 10, "Управление", "/10/");
    var child = new ExportSubdivision(childId, rootId, 20, "Отдел", "/10/20/");
    var tree = new ExportHierarchy([root, child]);
    check(tree.Get(childId).SequenceEqual(new[] { "Управление", "Отдел" }) && tree.Get(rootId).Length == 1 && tree.Get(null).Length == 0, "hierarchy includes root and direct subdivision with empty unassigned chain");
    foreach (var broken in new[] { child with { ParentId = childId }, child with { ParentId = Guid.NewGuid() }, child with { Path = "/99/20/" }, child with { PathId = 999 } }) {
      try { new ExportHierarchy([root, broken]).Get(childId); check(false, "broken hierarchy rejected"); }
      catch (InvalidOperationException) { check(true, "broken hierarchy rejected"); }
    }
    var job = new ExportJob { SecurityStamp = "stamp" };
    var scope = new ExportAccessScope(new("user", false, []), rootId, "/10/", [rootId], "stamp");
    ExportAccess.CheckDelivery(scope, job, [rootId], false); check(true, "unchanged delivery scope allowed");
    foreach (var changed in new[] { scope with { Stamp = "changed" }, scope with { AllowedIds = [] } }) {
      try { ExportAccess.CheckDelivery(changed, job, [rootId], false); check(false, "revoked scope or stamp blocks delivery"); }
      catch (UnauthorizedAccessException) { check(true, "revoked scope or stamp blocks delivery"); }
    }
    try { ExportAccess.CheckDelivery(scope, job, [], true); check(false, "unassigned records require Root at delivery"); }
    catch (UnauthorizedAccessException) { check(true, "unassigned records require Root at delivery"); }
    check(!new UserAccessSnapshot("user", false, [Permissions.Exports.ReadArchive]).Has(Permissions.Exports.ReadArchive), "archive remains Root-only even with forged individual permission value");
    var env = new EnvironmentStub(Path.Combine(Path.GetTempPath(), "guard-export-test-" + Guid.NewGuid().ToString("N")));
    Directory.CreateDirectory(env.ContentRootPath);
    var options = Options.Create(new ExportOptions { Directory = "archive", RowsPerPart = 2, MinimumFreeBytes = 0 });
    var storage = new ExportStorage(new EphemeralDataProtectionProvider(), options, env);
    try {
      var source = Path.Combine(env.ContentRootPath, "source.txt");
      var text = new string('Ж', 100000) + "=SUM(A1) SECRET"; await File.WriteAllTextAsync(source, text);
      var saved = await storage.SaveAsync(source, default);
      check(!Encoding.UTF8.GetString(await File.ReadAllBytesAsync(storage.ArtifactPath(saved.Key))).Contains("SECRET"), "archive is encrypted at rest");
      await storage.VerifyAsync(saved.Key, saved.Hash, saved.Size, default);
      await using (var input = storage.Open(saved.Key)) using (var output = new MemoryStream()) {
        await input.CopyToAsync(output); var original = await File.ReadAllBytesAsync(source);
        check(output.ToArray().SequenceEqual(original), "decrypted archive exactly matches delivered bytes across multiple chunks");
      }
      var bytes = await File.ReadAllBytesAsync(storage.ArtifactPath(saved.Key)); bytes[50] ^= 1; await File.WriteAllBytesAsync(storage.ArtifactPath(saved.Key), bytes);
      try { await storage.VerifyAsync(saved.Key, saved.Hash, saved.Size, default); check(false, "modified archive rejected"); }
      catch (CryptographicException) { check(true, "modified archive rejected"); }
      saved = await storage.SaveAsync(source, default); bytes = await File.ReadAllBytesAsync(storage.ArtifactPath(saved.Key));
      await File.WriteAllBytesAsync(storage.ArtifactPath(saved.Key), bytes[..^10]);
      try { await storage.VerifyAsync(saved.Key, saved.Hash, saved.Size, default); check(false, "truncated archive rejected"); }
      catch (EndOfStreamException) { check(true, "truncated archive rejected"); }
      try { storage.ArtifactPath("../escape"); check(false, "path traversal rejected"); }
      catch (ArgumentException) { check(true, "path traversal rejected"); }
      var badStorage = new ExportStorage(new EphemeralDataProtectionProvider(), Options.Create(new ExportOptions { Directory = "wwwroot/archive" }), env);
      try { badStorage.CheckSpace(); check(false, "public archive directory rejected"); }
      catch (InvalidOperationException) { check(true, "public archive directory rejected"); }
      var writer = new ExportDocumentWriter(storage, options);
      foreach (var area in Enum.GetValues<ExportArea>()) foreach (var format in Enum.GetValues<ExportFormat>()) foreach (var count in new[] { 0, 1, 3 }) {
        var item = new ExportJob { Area = area, Format = format, AuthorName = "Тестовый автор" };
        await WriteRowsAsync(storage, item.Id, area, count);
        var file = await writer.WriteAsync(item, new(area, format), new(count, 2, [childId], false),
          new(item.Id, ExportSchema.Title(area), item.AuthorName, item.CreatedAtUtc, "Europe/Moscow", "Тестовая выгрузка"), _ => Task.CompletedTask, default);
        var paths = new List<string>();
        if (file.ContentType == "application/zip") {
          using var archive = System.IO.Compression.ZipFile.OpenRead(file.Path);
          foreach (var entry in archive.Entries.Where(e => e.Name.EndsWith(".xlsx") || e.Name.EndsWith(".docx"))) {
            var path = Path.Combine(storage.WorkDirectory(item.Id), "qa-" + paths.Count); entry.ExtractToFile(path); paths.Add(path);
          }
        } else paths.Add(file.Path);
        check(paths.Count == (count > 2 ? 2 : 1), "large export splits without dropping parts");
        var total = 0;
        foreach (var path in paths) {
          using OpenXmlPackage package = format == ExportFormat.Excel ? SpreadsheetDocument.Open(path, false) : WordprocessingDocument.Open(path, false);
          var errors = new OpenXmlValidator().Validate(package).Take(5).ToArray();
          if (errors.Length > 0) throw new Exception(string.Join("; ", errors.Select(e => e.Description + " " + e.Path?.XPath)));
          check(errors.Length == 0, "generated Word/Excel validates against Open XML schema");
          var preview = DataExportService.ReadPreview(path, format, 0, default);
          check(preview.Headers.Contains(ExportSchema.Headers(area)[0]), "preview reads actual archived document headers");
          check(format != ExportFormat.Excel || preview.Headers.Take(2).SequenceEqual(new[] { "Уровень 1", "Уровень 2" }), "Excel ancestor columns precede DTO columns");
          if (count > 0) { total += preview.Rows.Count; check(preview.Rows.Any(r => r.Contains("=SUM(A1)")), "formula-like values remain literal text"); }
          else check(preview.Rows[0][0] == "Записи не найдены", "empty selection produces a readable document");
        }
        if (count > 0) check(total == count, "all export rows survive document partitioning");
      }
    }
    finally { Directory.Delete(env.ContentRootPath, true); }
  }
  public static async Task WriteRowsAsync(ExportStorage storage, Guid id, ExportArea area, int count)
  {
    await using var stream = File.Create(Path.Combine(storage.WorkDirectory(id), "rows.protected"));
    for (var i = 0; i < count; i++) {
      object dto = area switch {
        ExportArea.Personals => new PersonalExportDto("=SUM(A1)", "Иван", "", "Отдел", "Добавлено"),
        ExportArea.IpAddresses => new IpAddressExportDto("=SUM(A1)", "Описание", "Отдел", "Добавлено"),
        _ => new SubdivisionExportDto("=SUM(A1)", "Управление", "Тип органа", "Добавлено", "Наименование", "001234", "Адрес", "Телефон", 12.5, 2, true)
      };
      var row = new ExportRow(Guid.NewGuid(), ["Управление", "Отдел"], ExportSchema.Cells(area, dto));
      await storage.WriteFrameAsync(stream, "rows:" + id, i, JsonSerializer.SerializeToUtf8Bytes(row), default);
    }
    await storage.WriteFrameAsync(stream, "rows:" + id, count, ReadOnlyMemory<byte>.Empty, default);
  }
  public sealed class EnvironmentStub(string root) : IWebHostEnvironment
  {
    public string ApplicationName { get; set; } = "Guard.Export.Tests";
    public string EnvironmentName { get; set; } = "Testing";
    public string ContentRootPath { get; set; } = root;
    public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
  }
}
