using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Guard.Core.Services.Exports;

public sealed class ExportProcessor(IDbContextFactory<ApplicationDbContext> contexts, ExportAccess access, ExportSnapshot snapshot,
  ExportDocumentWriter writer, ExportStorage storage, IAuditService audit, IOptions<ExportOptions> options, ILogger<ExportProcessor> logger)
{
  public async Task<bool> RunNextAsync(CancellationToken stopping)
  {
    await using var db = await contexts.CreateDbContextAsync(stopping);
    var job = await db.ExportJobs.AsNoTracking().Where(j => j.State == ExportJobState.Queued).OrderBy(j => j.CreatedAtUtc).ThenBy(j => j.Id).FirstOrDefaultAsync(stopping);
    if (job == null) return false;
    var claimed = await db.ExportJobs.Where(j => j.Id == job.Id && j.State == ExportJobState.Queued)
      .ExecuteUpdateAsync(u => u.SetProperty(j => j.State, ExportJobState.Processing).SetProperty(j => j.StartedAtUtc, DateTimeOffset.UtcNow), stopping);
    if (claimed == 0) return true;
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping); timeout.CancelAfter(TimeSpan.FromMinutes(options.Value.TimeoutMinutes));
    var ct = timeout.Token; string? key = null; bool published = false;
    try {
      await CheckpointAsync(job.Id, ct);
      if (job.SchemaVersion != 1) throw new InvalidDataException("Неподдерживаемая версия схемы выгрузки.");
      var request = JsonSerializer.Deserialize<ExportRequest>(job.RequestJson) ?? throw new InvalidDataException("Запрос выгрузки повреждён.");
      DataExportService.Validate(request);
      var scope = await access.ForUserAsync(job.AuthorId, request, ct);
      if (scope.Stamp != job.SecurityStamp) throw new UnauthorizedAccessException("Сессия автора изменилась.");
      var captured = await snapshot.CaptureAsync(job, request, scope, token => CheckpointAsync(job.Id, token), ct);
      var info = new ExportDocumentInfo(job.Id, ExportSchema.Title(job.Area), job.AuthorName, DateTimeOffset.UtcNow, request.TimeZone, Conditions(request));
      var file = await writer.WriteAsync(job, request, captured, info, token => CheckpointAsync(job.Id, token), ct);
      var saved = await storage.SaveAsync(file.Path, ct); key = saved.Key;
      await CheckpointAsync(job.Id, ct);
      ExportAccess.CheckDelivery(await access.ForUserAsync(job.AuthorId, request, ct), job, captured.SubdivisionIds, captured.Unassigned);
      await using var transaction = await db.Database.BeginTransactionAsync(ct);
      db.ExportArtifacts.Add(new() { JobId = job.Id, StorageKey = saved.Key, FileName = file.Name, ContentType = file.ContentType,
        Sha256 = saved.Hash, Size = saved.Size, SubdivisionIdsJson = JsonSerializer.Serialize(captured.SubdivisionIds), IncludesUnassigned = captured.Unassigned });
      await db.SaveChangesAsync(ct);
      var ready = await db.ExportJobs.Where(j => j.Id == job.Id && j.State == ExportJobState.Processing && !j.CancelRequested && j.ExpiresAtUtc > DateTimeOffset.UtcNow)
        .ExecuteUpdateAsync(u => u.SetProperty(j => j.State, ExportJobState.Ready).SetProperty(j => j.RowCount, captured.Count).SetProperty(j => j.CompletedAtUtc, DateTimeOffset.UtcNow), ct);
      if (ready == 0) throw new OperationCanceledException("Выгрузка отменена.");
      await transaction.CommitAsync(ct); published = true;
      // Ready and its artifact are committed. Audit delivery cannot fail generation.
      try { audit.LogIdentityEvent(AuditEventType.ExportArchived, job.AuthorId, job.ClientIp, $"Export {job.Id}; rows {captured.Count}; SHA256 {saved.Hash}"); }
      catch (Exception ex) { logger.LogError(ex, "Выгрузка {ExportId} готова, но событие аудита {EventType} не доставлено", job.Id, AuditEventType.ExportArchived); }
    }
    catch (OperationCanceledException) when (stopping.IsCancellationRequested) { throw; }
    catch (Exception ex) {
      logger.LogError(ex, "Формирование выгрузки {ExportId} завершилось ошибкой", job.Id);
      var cancelled = await db.ExportJobs.Where(j => j.Id == job.Id).Select(j => j.CancelRequested).SingleAsync(stopping);
      var error = cancelled ? "Выгрузка отменена." : ex is UnauthorizedAccessException ? "Доступ изменился. Создайте новую выгрузку." :
        ex is OperationCanceledException ? "Превышено время формирования. Сузьте фильтры и повторите выгрузку." : "Не удалось сформировать выгрузку. Обратитесь к администратору.";
      await db.ExportJobs.Where(j => j.Id == job.Id && j.State == ExportJobState.Processing).ExecuteUpdateAsync(u =>
        u.SetProperty(j => j.State, cancelled ? ExportJobState.Cancelled : ExportJobState.Failed).SetProperty(j => j.Error, error).SetProperty(j => j.CompletedAtUtc, DateTimeOffset.UtcNow), stopping);
      audit.LogIdentityEvent(cancelled ? AuditEventType.ExportCancelled : AuditEventType.ExportFailed, job.AuthorId, job.ClientIp, $"Export {job.Id}");
    }
    finally {
      if (!published && key != null) try {
        // A failed commit acknowledgement does not prove that PostgreSQL rolled back.
        using var reconcile = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var check = await contexts.CreateDbContextAsync(reconcile.Token);
        if (!await check.ExportArtifacts.AnyAsync(a => a.StorageKey == key, reconcile.Token)) storage.Delete(key);
      } catch (Exception ex) { logger.LogWarning(ex, "Очистка незавершённой копии {ExportId}", job.Id); }
      try { storage.DeleteWork(job.Id); } catch (Exception ex) { logger.LogWarning(ex, "Очистка рабочих файлов {ExportId}", job.Id); }
    }
    return true;
  }
  private async Task CheckpointAsync(Guid id, CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested(); await using var db = await contexts.CreateDbContextAsync(ct);
    if (await db.ExportJobs.AnyAsync(j => j.Id == id && (j.CancelRequested || j.ExpiresAtUtc <= DateTimeOffset.UtcNow), ct)) throw new OperationCanceledException("Выгрузка отменена.");
  }
  public static string Conditions(ExportRequest request)
  {
    var mode = request.Mode switch { DataViewMode.Active => "активные", DataViewMode.Archived => "архивные", _ => "удалённые" };
    var hierarchy = request.AllSubdivisions ? "все доступные подразделения" : request.IncludeChildren ? "своё подразделение и подчинённые" : "только своё подразделение";
    string Field(string? name) => name switch {
      "LastName" => "Фамилия", "FirstName" => "Имя", "MiddleName" => "Отчество", "FullName" => "ФИО",
      "Subdivision.Name" => "Подразделение", "Name" => "Наименование", "Address" => request.Area == ExportArea.IpAddresses ? "IP-адрес" : "Адрес",
      "Description" => "Описание", "OrganType.Value" => "Тип органа", "Parent.Name" => "Родитель", "StatusClassifier.Value" => "Статус справочника",
      "InsertedDate" => "Дата добавления", "StaffCount" => "Штатная численность", "LevelOrder" => "Порядок", "IsDepartment" => "Отдел",
      "PostalCode" => "Почтовый индекс", "Phone" => "Телефон", "Fax" => "Факс", "PositionFormationName" => "Наименование для должности", _ => name ?? ""
    };
    string Operation(string? value) => value switch {
      "Equals" => "равно", "NotEquals" => "не равно", "Contains" => "содержит", "DoesNotContain" => "не содержит",
      "StartsWith" => "начинается с", "EndsWith" => "заканчивается на", "GreaterThan" => "больше", "GreaterThanOrEquals" => "не меньше",
      "LessThan" => "меньше", "LessThanOrEquals" => "не больше", "IsNull" => "не задано", "IsNotNull" => "задано",
      "IsEmpty" => "пусто", "IsNotEmpty" => "не пусто", "In" => "в списке", "NotIn" => "не в списке", _ => value ?? ""
    };
    string Describe(ExportFilter f) => f.Children != null ? "(" + string.Join(f.All ? " И " : " ИЛИ ", f.Children.Select(Describe)) + ")" :
      $"{Field(f.Field)} {Operation(f.Operation)} {f.Value?.ToString()}";
    string DescribePersonal(Guard.Core.Services.DTOs.PersonalFilter f) => f.Children != null ?
      "(" + string.Join(f.All ? " И " : " ИЛИ ", f.Children.Select(DescribePersonal)) + ")" : $"{Field(f.Field)} {Operation(f.Operation)} {f.Value}";
    var filter = request.PersonalFilter != null ? DescribePersonal(request.PersonalFilter) : request.Filter?.Children?.Count > 0 ? Describe(request.Filter) : "без дополнительных условий";
    return $"Статус: {mode}. Область: {hierarchy}. Фильтр: {filter}.";
  }
  public async Task RecoverAsync(CancellationToken ct)
  {
    await storage.ArchiveGate.WaitAsync(ct);
    try {
    await using var db = await contexts.CreateDbContextAsync(ct);
    await db.ExportJobs.Where(j => j.State == ExportJobState.Processing).ExecuteUpdateAsync(u => u.SetProperty(j => j.State, ExportJobState.Queued), ct);
    await db.ExportJobs.Where(j => j.State == ExportJobState.Delivering).ExecuteUpdateAsync(u => u.SetProperty(j => j.State, ExportJobState.Failed)
      .SetProperty(j => j.Error, "Передача прервана перезапуском сервера. Создайте новую выгрузку."), ct);
    await db.ExportDeliveryAttempts.Where(d => d.CompletedAtUtc == null).ExecuteUpdateAsync(u => u.SetProperty(d => d.CompletedAtUtc, DateTimeOffset.UtcNow), ct);
    foreach (var id in storage.WorkIds()) storage.DeleteWork(id);
    } finally { storage.ArchiveGate.Release(); }
  }
  public async Task CleanupAsync(CancellationToken ct, bool scanOrphans = false)
  {
    await storage.ArchiveGate.WaitAsync(ct);
    try {
      await using var db = await contexts.CreateDbContextAsync(ct);
      var cancelled = await db.ExportJobs.Where(j => j.CancelRequested && (j.State == ExportJobState.Ready || j.State == ExportJobState.Queued)).ToListAsync(ct);
      foreach (var job in cancelled) { job.State = ExportJobState.Cancelled; job.CompletedAtUtc = DateTimeOffset.UtcNow; }
      await db.SaveChangesAsync(ct);
      var expired = await db.ExportJobs.Include(j => j.Artifact).Where(j => j.ExpiresAtUtc <= DateTimeOffset.UtcNow &&
        j.State != ExportJobState.Processing && j.State != ExportJobState.Delivering && j.State != ExportJobState.Expired).Take(100).ToListAsync(ct);
      foreach (var job in expired) {
        ct.ThrowIfCancellationRequested();
        var actor = job.AuthorId;
        if (job.Artifact != null) storage.Delete(job.Artifact.StorageKey);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (job.Artifact != null) db.ExportArtifacts.Remove(job.Artifact);
        await db.ExportDeliveryAttempts.Where(d => d.JobId == job.Id).ExecuteDeleteAsync(ct);
        job.State = ExportJobState.Expired; job.RequestJson = "{}"; job.SecurityStamp = "";
        job.AuthorId = ""; job.AuthorName = "Удалено по сроку хранения"; job.ClientIp = null; job.RowCount = 0; job.Error = null;
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        audit.LogIdentityEvent(AuditEventType.ExportExpired, actor, details: $"Export {job.Id}");
      }
      if (scanOrphans) {
      foreach (var key in storage.Orphans(DateTimeOffset.UtcNow.AddDays(-1)))
        if (!await db.ExportArtifacts.AnyAsync(a => a.StorageKey == key && a.DeletedAtUtc == null, ct)) storage.Delete(key);
      foreach (var id in storage.WorkIds())
        if (!await db.ExportJobs.AnyAsync(j => j.Id == id && (j.State == ExportJobState.Queued || j.State == ExportJobState.Processing), ct)) storage.DeleteWork(id);
      }
    }
    finally { storage.ArchiveGate.Release(); }
  }
}
