using Guard.Core.Contexts;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Logging;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace Guard.Core.Services;

public sealed class TechnicalLogService(IPermissionService permissions, IOperationAvailability availability,
  TechnicalLogRuntime runtime, TechnicalLogBuffer buffer, IAuditService audit, IDbContextFactory<LogsDbContext> factory) : ITechnicalLogService
{
  private static readonly SemaphoreSlim gate = new(1, 1);
  private static readonly SemaphoreSlim exportGate = new(1, 1);
  private async Task<UserAccessSnapshot> RequireAsync(string permission, CancellationToken ct)
  {
    await availability.RequireWorkingAsync(ct);
    var actor = await SearchAccess.RequireAsync(permissions, permission, ct);
    if (!actor.IsRoot || string.IsNullOrWhiteSpace(actor.UserId)) throw new UnauthorizedAccessException();
    return actor;
  }
  private TechnicalLogState State() { var state = runtime.Read(); return new(state.Version, state.Settings, state.Session, buffer.Health(), runtime.InstanceId); }
  public async Task<TechnicalLogState> GetAsync(CancellationToken ct = default)
  { using var quiet = Serilog.Context.LogContext.PushProperty("TechnicalLogInternal", true); await RequireAsync(Permissions.Logs.Read, ct); return State(); }
  public Task<TechnicalLogState> SaveAsync(Guid version, TechnicalLogSettings settings, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(settings); settings.Validate();
    return ChangeAsync(version, "SettingsChanged", current => (settings, current.Session), ct);
  }
  public Task<TechnicalLogState> StartAsync(Guid version, DiagnosticLogRequest request, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    return ChangeAsync(version, "DiagnosticStarted", current => {
      if (request.Minutes < 1 || request.Minutes > current.Settings.MaximumDiagnosticMinutes)
        throw new ArgumentException("Укажите длительность в пределах установленного максимума.");
      if (request.Level is not (ApplicationLogLevel.Debug or ApplicationLogLevel.Verbose) || request.Category == null || request.Reason == null)
        throw new ArgumentException("Укажите уровень, категорию и основание диагностики.");
      return (current.Settings, new DiagnosticLogSession(Guid.NewGuid(), "", request.Level, request.Category.Trim(),
        TechnicalLogPrivacy.Text(request.Reason.Trim(), 500), runtime.UtcNow, runtime.UtcNow.AddMinutes(request.Minutes)));
    }, ct);
  }
  public Task<TechnicalLogState> StopAsync(Guid version, CancellationToken ct = default) => ChangeAsync(version, "DiagnosticStopped", current => (current.Settings, null), ct);
  private async Task<TechnicalLogState> ChangeAsync(Guid version, string action,
    Func<(Guid Version, TechnicalLogSettings Settings, DiagnosticLogSession? Session), (TechnicalLogSettings Settings, DiagnosticLogSession? Session)> command,
    CancellationToken ct)
  {
    var actor = await RequireAsync(Permissions.Logs.Configure, ct);
    await gate.WaitAsync(ct);
    try {
      var current = runtime.Read();
      if (current.Version != version) throw new InvalidOperationException("Состояние изменилось. Обновите страницу перед повторением.");
      var next = command(current);
      if (action == "DiagnosticStarted") next.Session = next.Session! with { ActorId = actor.UserId! };
      var intent = AuditEventData.Create(AuditEventType.LoggingConfigurationChangeRequested, actor.UserId,
        details: JsonSerializer.Serialize(new { Action = action, Version = version, PreviousSettings = current.Settings,
          Settings = next.Settings, PreviousSessionId = current.Session?.Id, SessionId = next.Session?.Id,
          Category = next.Session?.Category, ExpiresAtUtc = next.Session?.ExpiresAtUtc, Reason = next.Session?.Reason }))
        with { ObjectType = "TechnicalLogging", ObjectId = runtime.InstanceId, Outcome = "started" };
      await audit.RecordAsync(intent, ct);
      await SearchAccess.RecheckAsync(permissions, Permissions.Logs.Configure, actor, ct);
      try { runtime.Change(version, next.Settings, next.Session); }
      catch {
        await audit.RecordAsync(intent with { EventId = Guid.NewGuid(), EventType = AuditEventType.LoggingConfigurationChanged,
          Outcome = "failure", ReasonCode = "ConfigurationApplyFailed" }, ct); throw;
      }
      try {
        await audit.RecordAsync(intent with { EventId = Guid.NewGuid(), EventType = AuditEventType.LoggingConfigurationChanged,
          Outcome = "success", ReasonCode = action }, ct);
      } catch (Exception error) { throw new OperationCommitUnknownException(intent.CorrelationId, error); }
      return State();
    } finally { gate.Release(); }
  }
  public async Task<IReadOnlyList<TechnicalErrorGroup>> ErrorsAsync(CancellationToken ct = default)
  {
    using var quiet = Serilog.Context.LogContext.PushProperty("TechnicalLogInternal", true);
    var actor = await RequireAsync(Permissions.Logs.Read, ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    db.Database.SetCommandTimeout(5);
    var cutoff = runtime.UtcNow.AddMinutes(-runtime.Read().Settings.ErrorWindowMinutes);
    var groups = await db.Logs.AsNoTracking().Where(e => e.Timestamp >= cutoff && (e.Level == "Error" || e.Level == "Fatal") && e.Fingerprint != null)
      .GroupBy(e => new { e.Fingerprint, e.SourceContext })
      .Select(g => new { Fingerprint = g.Key.Fingerprint!, Category = g.Key.SourceContext, Count = g.LongCount(), FirstUtc = g.Min(e => e.Timestamp), LastUtc = g.Max(e => e.Timestamp) })
      .OrderByDescending(g => g.Count).ThenBy(g => g.Fingerprint).ThenBy(g => g.Category).Take(20).ToListAsync(ct);
    await SearchAccess.RecheckAsync(permissions, Permissions.Logs.Read, actor, ct);
    return groups.Select(g => new TechnicalErrorGroup(g.Fingerprint, g.Category, g.Count, g.FirstUtc, g.LastUtc)).ToArray();
  }
  public async Task<TechnicalLogExport> ExportAsync(TechnicalLogExportRequest request, CancellationToken ct = default)
  {
    var actor = await RequireAsync(Permissions.Logs.Read, ct);
    ArgumentNullException.ThrowIfNull(request);
    if (request.FromUtc.Kind != DateTimeKind.Utc || request.ToUtc.Kind != DateTimeKind.Utc || request.FromUtc >= request.ToUtc ||
      request.ToUtc - request.FromUtc > TimeSpan.FromDays(7) || request.CorrelationId?.Length > 200 || !Enum.IsDefined(request.Source) ||
      request.Level is not (null or "Verbose" or "Debug" or "Information" or "Warning" or "Error" or "Fatal") ||
      request.AfterCursor != null && (request.Source != TechnicalLogSource.LocalBuffer || request.AfterCursor.Length != 52 ||
        !System.Text.RegularExpressions.Regex.IsMatch(request.AfterCursor, "^[0-9]{19}-[0-9a-f]{32}$")))
      throw new ArgumentException("Выберите период UTC длительностью не более 7 дней.");
    var id = Guid.NewGuid();
    var entry = AuditEventData.Create(AuditEventType.ExportRequested, actor.UserId,
      details: $"Source={request.Source}; FromUtc={request.FromUtc:O}; ToUtc={request.ToUtc:O}; DiagnosticSessionId={request.DiagnosticSessionId}; Level={request.Level}")
      with { ObjectType = "TechnicalLogExport", ObjectId = id.ToString() };
    await audit.RecordAsync(entry, ct);
    if (!await exportGate.WaitAsync(0, ct)) {
      await audit.RecordAsync(entry with { EventId = Guid.NewGuid(), EventType = AuditEventType.ExportFailed,
        Outcome = "failure", ReasonCode = "TechnicalExportBusy" }, ct);
      throw new InvalidOperationException("Выгрузка технических логов уже выполняется. Повторите позже.");
    }
    try {
      using var quiet = Serilog.Context.LogContext.PushProperty("TechnicalLogInternal", true);
      using var stream = new MemoryStream(); var count = 0; var truncated = false;
      int? scanned = null; var unreadable = 0; string? nextCursor = null;
      if (request.Source == TechnicalLogSource.LocalBuffer) {
        var snapshot = buffer.Snapshot(request, ct); truncated = snapshot.Truncated;
        scanned = snapshot.Scanned; unreadable = snapshot.Unreadable; nextCursor = snapshot.NextCursor;
        string? includedCursor = request.AfterCursor;
        foreach (var item in snapshot.Records) {
          if (!Append(item.Record)) { nextCursor = includedCursor; break; }
          includedCursor = item.Cursor;
        }
      } else {
        await using var db = await factory.CreateDbContextAsync(ct); db.Database.SetCommandTimeout(10);
        // jsonb нельзя обрезать строковым Substring. Ограничиваем размер до передачи исторического payload клиенту.
        var query = db.Logs.FromSqlRaw("""
          SELECT id,timestamp,level,layer,message,message_template,exception,user_id,ip_address,
            correlation_id,source_context,diagnostic_session_id,run_id,fingerprint,application_version,instance_id,
            CASE WHEN octet_length(properties::text) > 65536 THEN jsonb_build_object('PropertiesTruncated', true) ELSE properties END AS properties
          FROM public.logs
          """).AsNoTracking().Where(e => e.Timestamp >= request.FromUtc && e.Timestamp < request.ToUtc);
        if (!string.IsNullOrWhiteSpace(request.CorrelationId)) query = query.Where(e => e.CorrelationId == request.CorrelationId);
        if (request.DiagnosticSessionId != null) query = query.Where(e => e.DiagnosticSessionId == request.DiagnosticSessionId);
        if (request.Level != null) query = query.Where(e => e.Level == request.Level);
        var rows = query.OrderBy(e => e.Timestamp).ThenBy(e => e.Id).Take(10001).Select(e => new TechnicalLogRecord(
          e.Id, e.Timestamp, e.Level ?? "", e.Layer ?? "",
          e.Message == null ? "" : e.Message.Substring(0, Math.Min(e.Message.Length, 8192)),
          e.MessageTemplate == null ? "" : e.MessageTemplate.Substring(0, Math.Min(e.MessageTemplate.Length, 2048)),
          e.Exception == null ? null : e.Exception.Substring(0, Math.Min(e.Exception.Length, 8192)),
          e.Properties ?? "{}", null, null,
          e.CorrelationId == null ? null : e.CorrelationId.Substring(0, Math.Min(e.CorrelationId.Length, 1024)),
          e.SourceContext == null ? null : e.SourceContext.Substring(0, Math.Min(e.SourceContext.Length, 1024)), e.DiagnosticSessionId,
          e.RunId == null ? null : e.RunId.Substring(0, Math.Min(e.RunId.Length, 1024)),
          e.Fingerprint == null ? "" : e.Fingerprint.Substring(0, Math.Min(e.Fingerprint.Length, 128)),
          e.ApplicationVersion == null ? "" : e.ApplicationVersion.Substring(0, Math.Min(e.ApplicationVersion.Length, 128)),
          e.InstanceId == null ? "" : e.InstanceId.Substring(0, Math.Min(e.InstanceId.Length, 256))));
        await foreach (var item in rows.AsAsyncEnumerable().WithCancellation(ct)) if (!Append(item)) break;
      }
      await SearchAccess.RecheckAsync(permissions, Permissions.Logs.Read, actor, ct);
      await availability.RequireWorkingAsync(ct);
      await audit.RecordAsync(entry with { EventId = Guid.NewGuid(), EventType = AuditEventType.ExportArchived,
        Details = $"Count={count}; Truncated={truncated}", Outcome = "success" }, ct);
      var manifest = JsonSerializer.SerializeToUtf8Bytes(new { RecordType = "ExportManifest", Id = id, Count = count,
        Truncated = truncated, request.FromUtc, request.ToUtc, request.CorrelationId, request.DiagnosticSessionId,
        Source = request.Source.ToString(), request.Level, Scanned = scanned, Unreadable = unreadable, NextCursor = nextCursor,
        Snapshot = request.Source == TechnicalLogSource.LocalBuffer ? "PendingOnly; concurrent delivery may remove events; scan limited to first 1000 files / 8MiB" : "Database",
        runtime.InstanceId, Settings = runtime.Read().Settings, Health = buffer.Health() });
      if (manifest.Length > 4095) throw new InvalidOperationException("Описание выгрузки превышает допустимый размер.");
      using var package = new MemoryStream(); await package.WriteAsync(manifest, ct); package.WriteByte((byte)'\n');
      stream.Position = 0; await stream.CopyToAsync(package, ct);
      await SearchAccess.RecheckAsync(permissions, Permissions.Logs.Read, actor, ct);
      await availability.RequireWorkingAsync(ct);
      return new($"Guard-technical-{id:N}.jsonl", package.ToArray(), count, truncated, id);
      bool Append(TechnicalLogRecord item)
      {
        ct.ThrowIfCancellationRequested();
        var line = ExportLine(item);
        if (count == 10000 || stream.Length + line.Length + 1 > 8 * 1024 * 1024 - 4096) { truncated = true; return false; }
        stream.Write(line); stream.WriteByte((byte)'\n'); count++; return true;
      }
    } catch {
      await audit.RecordAsync(entry with { EventId = Guid.NewGuid(), EventType = AuditEventType.ExportFailed, Outcome = "failure" }, ct); throw;
    }
    finally { exportGate.Release(); }
  }
  private static byte[] ExportLine(TechnicalLogRecord item)
  {
    string? Safe(string? text, int size = 1024) => text == null ? null : TechnicalLogPrivacy.Text(text, size);
    return JsonSerializer.SerializeToUtf8Bytes(new { item.Id, item.Timestamp,
      Level = Safe(item.Level, 32), Layer = Safe(item.Layer, 50), Message = Safe(item.Message, 8192),
      MessageTemplate = Safe(item.MessageTemplate, 2048), Exception = TechnicalException.ForExport(item.Exception),
      Properties = TechnicalLogPrivacy.ExportProperties(item.Properties),
      CorrelationId = Safe(item.CorrelationId), SourceContext = Safe(item.SourceContext), item.DiagnosticSessionId,
      RunId = Safe(item.RunId), Fingerprint = Safe(item.Fingerprint, 128),
      ApplicationVersion = Safe(item.ApplicationVersion, 128), InstanceId = Safe(item.InstanceId, 256) });
  }
}
