using System.IO.Compression;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using S = DocumentFormat.OpenXml.Spreadsheet;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Guard.Core.Services.Exports;

public sealed class DataExportService(IDbContextFactory<ApplicationDbContext> contexts, IPermissionService permissions,
  ExportAccess access, ExportStorage storage, IOptions<ExportOptions> options, IAuditService audit, IHttpContextAccessor http) : IDataExportService
{
  public static void Validate(ExportRequest request)
  {
    if (!Enum.IsDefined(request.Area) || !Enum.IsDefined(request.Format) || !Enum.IsDefined(request.Mode)) throw new ArgumentException("Некорректные параметры выгрузки.");
    _ = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZone);
    if (JsonSerializer.SerializeToUtf8Bytes(request).Length > 32768) throw new ArgumentException("Слишком большой запрос выгрузки.");
    switch (request.Area) {
      case ExportArea.Personals:
        if (request.Filter != null) throw new ArgumentException("Некорректный фильтр сотрудников.");
        _ = PersonalListQuery.Sort(PersonalQueryFilter.Apply(Array.Empty<Personal>().AsQueryable(), request.PersonalFilter), request.OrderBy); break;
      case ExportArea.Subdivisions:
        if (request.PersonalFilter != null) throw new ArgumentException("Некорректный фильтр подразделений.");
        _ = ExportQueries.Sort(ExportFilters.Apply(Array.Empty<Subdivision>().AsQueryable(), request.Filter, ExportQueries.SubdivisionFields), request.OrderBy, ExportQueries.SubdivisionFields); break;
      case ExportArea.IpAddresses:
        if (request.PersonalFilter != null) throw new ArgumentException("Некорректный фильтр IP-адресов.");
        _ = ExportQueries.Sort(ExportFilters.Apply(Array.Empty<IpAddress>().AsQueryable(), request.Filter, ExportQueries.IpFields), request.OrderBy, ExportQueries.IpFields); break;
    }
  }
  public async Task<Guid> CreateAsync(ExportRequest request, CancellationToken ct = default)
  {
    Validate(request); var actor = await permissions.GetCurrentAsync(ct);
    var scope = await access.ForUserAsync(actor.UserId, request, ct); storage.CheckSpace();
    await using var db = await contexts.CreateDbContextAsync(ct);
    await using var transaction = await db.Database.BeginTransactionAsync(ct);
    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({actor.UserId}, 8181))", ct);
    if (await db.ExportJobs.CountAsync(j => j.AuthorId == actor.UserId && (j.State == ExportJobState.Queued || j.State == ExportJobState.Processing || j.State == ExportJobState.Ready), ct) >= options.Value.MaxQueuedPerUser)
      throw new EntityRuleException("Уже есть незавершённые выгрузки. Завершите или отмените их.");
    var name = await db.Users.Where(u => u.Id == actor.UserId).Select(u => u.UserName).SingleAsync(ct);
    var retention = await db.ExportRetentionSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == 1, ct);
    var job = new ExportJob { AuthorId = actor.UserId, AuthorName = name ?? actor.UserId, SecurityStamp = scope.Stamp,
      Area = request.Area, Format = request.Format, RequestJson = JsonSerializer.Serialize(request),
      ExpiresAtUtc = DateTimeOffset.UtcNow.AddYears(retention?.Years ?? 3),
      ClientIp = http.HttpContext == null ? null : ClientIpAddress.Get(http.HttpContext) };
    db.ExportJobs.Add(job); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    audit.LogIdentityEvent(AuditEventType.ExportRequested, actor.UserId, job.ClientIp, $"Export {job.Id}; {job.Area}; {job.Format}");
    return job.Id;
  }
  public async Task<ExportJobDto> GetAsync(Guid id, CancellationToken ct = default)
  {
    var actor = await permissions.GetCurrentAsync(ct); await using var db = await contexts.CreateDbContextAsync(ct);
    var job = await db.ExportJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id && j.AuthorId == actor.UserId, ct)
      ?? throw new KeyNotFoundException("Выгрузка не найдена.");
    return new(job.Id, job.State, job.RowCount, job.Error, job.CreatedAtUtc);
  }
  public async Task CancelAsync(Guid id, CancellationToken ct = default)
  {
    var actor = await permissions.GetCurrentAsync(ct); await using var db = await contexts.CreateDbContextAsync(ct);
    var changed = await db.ExportJobs.Where(j => j.Id == id && j.AuthorId == actor.UserId &&
      (j.State == ExportJobState.Queued || j.State == ExportJobState.Processing || j.State == ExportJobState.Ready))
      .ExecuteUpdateAsync(u => u.SetProperty(j => j.CancelRequested, true), ct);
    if (changed == 0) throw new EntityRuleException("Выгрузку уже нельзя отменить.");
    audit.LogIdentityEvent(AuditEventType.ExportCancelled, actor.UserId, details: $"Export {id}");
  }
  public async Task<IReadOnlyList<ExportJobDto>> GetActiveAsync(ExportArea area, CancellationToken ct = default)
  {
    var actor = await permissions.GetCurrentAsync(ct); await using var db = await contexts.CreateDbContextAsync(ct);
    return await db.ExportJobs.AsNoTracking().Where(j => j.AuthorId == actor.UserId && j.Area == area && !j.CancelRequested &&
      (j.State == ExportJobState.Queued || j.State == ExportJobState.Processing || j.State == ExportJobState.Ready || j.State == ExportJobState.Delivering))
      .OrderByDescending(j => j.CreatedAtUtc).Select(j => new ExportJobDto(j.Id, j.State, j.RowCount, j.Error, j.CreatedAtUtc)).ToListAsync(ct);
  }
  public async Task<ExportArchivePage> SearchArchiveAsync(ExportArchiveRequest request, CancellationToken ct = default)
  {
    await RequireRootAsync(Permissions.Exports.ReadArchive, ct);
    if (request.Skip < 0 || request.Take is < 1 or > 100) throw new ArgumentException("Некорректные параметры поиска.");
    ExportArchiveQuery.ValidateFilter(request.Filter);
    await using var db = await contexts.CreateDbContextAsync(ct); var q = db.ExportJobs.AsNoTracking();
    q = ExportFilters.Apply(q, request.Filter, ExportArchiveQuery.Fields);
    var count = await q.CountAsync(ct);
    var rows = await ExportQueries.Sort(q, string.IsNullOrWhiteSpace(request.OrderBy) ? "CreatedAtUtc desc" : request.OrderBy, ExportArchiveQuery.Fields).Skip(request.Skip).Take(request.Take)
      .Select(j => new ExportArchiveItem(j.Id, j.AuthorName, j.ClientIp, j.Area, j.Format, j.State, j.CreatedAtUtc, j.ExpiresAtUtc,
        j.RowCount, j.Artifact == null ? null : j.Artifact.Size, j.Artifact == null ? null : j.Artifact.Sha256)).ToListAsync(ct);
    await RequireRootAsync(Permissions.Exports.ReadArchive, ct);
    return new(rows, count);
  }
  internal async Task<string> RequireRootAsync(string permission, CancellationToken ct)
  {
    await permissions.RequireAsync(permission, ct); var actor = await permissions.GetCurrentAsync(ct);
    if (!actor.IsRoot) throw new UnauthorizedAccessException("Операция доступна только Root."); return actor.UserId;
  }
  public async Task<ExportArchiveDetails> GetArchiveDetailsAsync(Guid id, CancellationToken ct = default)
  {
    var actor = await RequireRootAsync(Permissions.Exports.ReadArchive, ct);
    await using var db = await contexts.CreateDbContextAsync(ct);
    var job = await db.ExportJobs.AsNoTracking().Include(j => j.Artifact).Include(j => j.Delivery).SingleOrDefaultAsync(j => j.Id == id, ct)
      ?? throw new KeyNotFoundException("Выгрузка не найдена.");
    var conditions = job.State == ExportJobState.Expired ? "Срок хранения истёк, параметры удалены." :
      ExportProcessor.Conditions(JsonSerializer.Deserialize<ExportRequest>(job.RequestJson) ?? throw new InvalidDataException("Параметры повреждены."));
    await RequireRootAsync(Permissions.Exports.ReadArchive, ct);
    audit.LogIdentityEvent(AuditEventType.ExportArchiveViewed, actor, details: $"Export metadata {id}");
    return new(conditions, job.Artifact?.Sha256, job.Artifact?.Size, job.Delivery?.StartedAtUtc, job.Delivery?.CompletedAtUtc, job.Delivery?.Succeeded);
  }
  public async Task<ExportRetentionDto> GetRetentionAsync(CancellationToken ct = default)
  {
    await RequireRootAsync(Permissions.Exports.ReadArchive, ct); await using var db = await contexts.CreateDbContextAsync(ct);
    var setting = await db.ExportRetentionSettings.AsNoTracking().Where(s => s.Id == 1)
      .Select(s => new ExportRetentionDto(s.Years, s.Version)).SingleOrDefaultAsync(ct) ?? new(3, Guid.Empty);
    await RequireRootAsync(Permissions.Exports.ReadArchive, ct);
    return setting;
  }
  public async Task SaveRetentionAsync(int years, Guid version, CancellationToken ct = default)
  {
    var actor = await RequireRootAsync(Permissions.Exports.Configure, ct);
    if (years is < 1 or > 30) throw new ArgumentException("Укажите срок от 1 до 30 лет.");
    await using var db = await contexts.CreateDbContextAsync(ct); var setting = await db.ExportRetentionSettings.SingleOrDefaultAsync(s => s.Id == 1, ct);
    if (setting == null) { setting = new(); db.ExportRetentionSettings.Add(setting); }
    else if (setting.Version != version) throw new DbUpdateConcurrencyException("Настройка уже изменена.");
    setting.Years = years; setting.Version = Guid.NewGuid(); setting.ModifiedBy = actor; setting.ModifiedAtUtc = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(ct); audit.LogIdentityEvent(AuditEventType.ExportRetentionChanged, actor, details: $"Years {years}");
  }
  public async Task<ExportPreview> PreviewAsync(Guid id, int part = 1, int skip = 0, CancellationToken ct = default)
  {
    var actor = await RequireRootAsync(Permissions.Exports.ReadArchive, ct);
    if (part < 1 || skip < 0 || skip > options.Value.RowsPerPart) throw new ArgumentException("Некорректная страница просмотра.");
    await using var db = await contexts.CreateDbContextAsync(ct);
    var file = await db.ExportArtifacts.AsNoTracking().SingleOrDefaultAsync(f => f.JobId == id && f.DeletedAtUtc == null && f.Job.ExpiresAtUtc > DateTimeOffset.UtcNow, ct)
      ?? throw new KeyNotFoundException("Архивная копия недоступна.");
    var previewId = Guid.NewGuid();
    await storage.ArchiveGate.WaitAsync(ct);
    try {
      var work = storage.WorkDirectory(previewId);
      await storage.VerifyAsync(file.StorageKey, file.Sha256, file.Size, ct);
      var path = Path.Combine(work, "preview");
      await using (var input = storage.Open(file.StorageKey)) await using (var output = File.Create(path)) await input.CopyToAsync(output, ct);
      string documentPath = path;
      if (file.ContentType == "application/zip") {
        using var zip = ZipFile.OpenRead(path);
        var entry = zip.Entries.Where(e => e.Name.EndsWith(".xlsx") || e.Name.EndsWith(".docx")).OrderBy(e => e.Name).Skip(part - 1).FirstOrDefault()
          ?? throw new ArgumentException("Часть выгрузки не найдена.");
        documentPath = Path.Combine(work, "document"); entry.ExtractToFile(documentPath);
      } else if (part != 1) throw new ArgumentException("У выгрузки только одна часть.");
      var format = await db.ExportJobs.Where(j => j.Id == id).Select(j => j.Format).SingleAsync(ct);
      var result = ReadPreview(documentPath, format, skip, ct);
      await RequireRootAsync(Permissions.Exports.ReadArchive, ct);
      audit.LogIdentityEvent(AuditEventType.ExportArchiveViewed, actor, details: $"Export {id}; part {part}; offset {skip}");
      return result;
    }
    finally { try { storage.DeleteWork(previewId); } finally { storage.ArchiveGate.Release(); } }
  }
  public static ExportPreview ReadPreview(string path, ExportFormat format, int skip, CancellationToken ct)
  {
    var lines = new List<IReadOnlyList<string>>(); int index = -1; string[] headers = []; bool more = false;
    using OpenXmlPackage package = format == ExportFormat.Excel ? SpreadsheetDocument.Open(path, false) : WordprocessingDocument.Open(path, false);
    var part = format == ExportFormat.Excel ? (OpenXmlPart)((SpreadsheetDocument)package).WorkbookPart!.WorksheetParts.First() : ((WordprocessingDocument)package).MainDocumentPart!;
    using var reader = OpenXmlReader.Create(part);
    while (reader.Read()) {
      ct.ThrowIfCancellationRequested();
      if (!reader.IsStartElement || reader.ElementType != (format == ExportFormat.Excel ? typeof(S.Row) : typeof(W.TableRow))) continue;
      var row = reader.LoadCurrentElement() ?? throw new InvalidDataException("Строка документа повреждена.");
      var cells = format == ExportFormat.Excel ? row.Elements<S.Cell>().Select(c => c.InlineString?.InnerText ?? (c.DataType?.Value == S.CellValues.Boolean ? c.CellValue?.Text == "1" ? "Да" : "Нет" : c.CellValue?.Text ?? "")).ToArray() :
        row.Elements<W.TableCell>().Select(c => string.Join(" ", c.Descendants<W.Text>().Select(t => t.Text))).ToArray();
      if (index == -1) { headers = cells; index++; continue; }
      if (index++ < skip) continue;
      if (lines.Count == 100) { more = true; break; } lines.Add(cells);
    }
    return new(headers, lines, more);
  }
}
