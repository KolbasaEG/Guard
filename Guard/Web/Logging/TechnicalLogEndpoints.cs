using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Antiforgery;
using System.Globalization;

namespace Guard.Web.Logging;

public static class TechnicalLogEndpoints
{
  public static IEndpointConventionBuilder MapTechnicalLogEndpoints(this IEndpointRouteBuilder routes) =>
    routes.MapPost("/technical-logs/export", DownloadAsync).RequireAuthorization(Permissions.Logs.Read).DisableAntiforgery();

  // Проверка токена выполняется явно до чтения параметров и до формирования файла.
  private static async Task DownloadAsync(HttpContext http, IAntiforgery antiforgery, ITechnicalLogService service,
    IPermissionService permissions, IOperationAvailability availability, IAuditService audit, ILoggerFactory loggers)
  {
    var ct = http.RequestAborted;
    AuditEventData? started = null;
    try {
      await antiforgery.ValidateRequestAsync(http);
      if (http.Request.Headers.ContainsKey("Range")) throw new ArgumentException("Частичная выгрузка не поддерживается.");
      var form = await http.Request.ReadFormAsync(ct);
      var from = DateTime.Parse(form["fromUtc"].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
      var to = DateTime.Parse(form["toUtc"].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
      Guid? session = string.IsNullOrEmpty(form["sessionId"]) ? null : Guid.Parse(form["sessionId"].ToString());
      var source = string.IsNullOrEmpty(form["source"]) ? TechnicalLogSource.Database : Enum.Parse<TechnicalLogSource>(form["source"].ToString(), false);
      var level = string.IsNullOrEmpty(form["level"]) ? null : form["level"].ToString();
      var after = string.IsNullOrEmpty(form["afterCursor"]) ? null : form["afterCursor"].ToString();
      var file = await service.ExportAsync(new(from, to, form["correlationId"], session, source, level, after), ct);
      var actor = await permissions.GetCurrentAsync(ct);
      if (!actor.IsRoot || !actor.Has(Permissions.Logs.Read)) throw new UnauthorizedAccessException();
      await availability.RequireWorkingAsync(ct);
      started = AuditEventData.Create(AuditEventType.ExportDeliveryStarted, actor.UserId,
        details: $"Count={file.Count}; Bytes={file.Content.Length}; Truncated={file.Truncated}")
        with { ObjectType = "TechnicalLogExport", ObjectId = file.Id.ToString(), Outcome = "started" };
      await audit.RecordAsync(started, ct);
      var current = await permissions.GetCurrentAsync(ct);
      if (current.UserId != actor.UserId || !current.IsRoot || !current.Has(Permissions.Logs.Read)) throw new UnauthorizedAccessException();
      await availability.RequireWorkingAsync(ct);
      http.Response.ContentType = "application/x-ndjson; charset=utf-8";
      http.Response.ContentLength = file.Content.Length;
      http.Response.Headers.CacheControl = "no-store, private";
      http.Response.Headers["X-Content-Type-Options"] = "nosniff";
      http.Response.Headers["X-Guard-Export-Truncated"] = file.Truncated ? "true" : "false";
      http.Response.Headers["X-Guard-Export-Count"] = file.Count.ToString(CultureInfo.InvariantCulture);
      http.Response.Headers.ContentDisposition = "attachment; filename=" + file.FileName;
      await http.Response.Body.WriteAsync(file.Content, ct); await http.Response.Body.FlushAsync(ct);
      await audit.RecordAsync(started with { EventId = Guid.NewGuid(), EventType = AuditEventType.ExportDeliveryCompleted,
        Outcome = "success", ReasonCode = "ServerWriteCompleted" }, ct);
    } catch (Exception error) {
      var logger = loggers.CreateLogger("Guard.TechnicalLogs.Export");
      if (error is OperationCanceledException && ct.IsCancellationRequested) logger.LogDebug("Передача технических логов отменена клиентом");
      else if (error is AntiforgeryValidationException) logger.LogWarning("Отклонён запрос выгрузки технических логов");
      else Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(logger, error, "Выгрузка технических логов не завершена");
      if (started != null) {
        try { await audit.RecordAsync(started with { EventId = Guid.NewGuid(), EventType = AuditEventType.ExportFailed,
          Outcome = "failure", ReasonCode = "DeliveryFailed" }, ct); }
        catch (Exception auditError) { loggers.CreateLogger("Guard.TechnicalLogs.Export").LogError(auditError, "Не подтверждён результат передачи технических логов"); }
      }
      if (!http.Response.HasStarted && !ct.IsCancellationRequested) {
        http.Response.Clear(); http.Response.Headers.CacheControl = "no-store";
        http.Response.StatusCode = error is AntiforgeryValidationException or UnauthorizedAccessException ? 403 :
          error is AuditUnavailableException or OperationCommitUnknownException ? 503 : 409;
        await http.Response.WriteAsync("Выгрузка недоступна. Проверьте доступ, состояние аудита и период (UTC, не более 7 дней).", ct);
      } else http.Abort();
    }
  }
}
