using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Microsoft.AspNetCore.Antiforgery;

namespace Guard.Web.Exports;

public static class ExportEndpoints
{
  public static IEndpointConventionBuilder MapExportEndpoints(this IEndpointRouteBuilder routes) =>
    routes.MapPost("/exports/{id:guid}/download", DownloadAsync).RequireAuthorization();

  private static async Task DownloadAsync(Guid id, HttpContext http, IAntiforgery antiforgery,
    IExportDeliveryService delivery, IAuditService audit, ILoggerFactory logs)
  {
    var ct = http.RequestAborted;
    var logger = logs.CreateLogger("Guard.Core.Services.Exports.Delivery");
    var delegated = false;
    try {
      await antiforgery.ValidateRequestAsync(http);
      if (http.Request.Headers.ContainsKey("Range")) throw new ArgumentException("Частичное скачивание не поддерживается.");
      delegated = true;
      await delivery.DeliverAsync(id, async (file, stream, token) => {
        http.Response.ContentType = file.ContentType; http.Response.ContentLength = file.Size;
        http.Response.Headers.CacheControl = "no-store, private";
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";
        http.Response.Headers.ContentDisposition = "attachment; filename*=UTF-8''" + Uri.EscapeDataString(file.FileName);
        await stream.CopyToAsync(http.Response.Body, token);
        await http.Response.Body.FlushAsync(token);
      }, ct);
    }
    catch (Exception ex) {
      logger.LogWarning(ex, "Передача выгрузки {ExportId} не завершена", id);
      // Ошибки до вызова сервиса не проходят через его аудит.
      if (!delegated) {
        try { audit.LogIdentityEvent(AuditEventType.ExportFailed, null, null, $"Export {id}"); }
        catch (Exception auditError) {
          logger.LogError(auditError, "Событие аудита {EventType} для передачи выгрузки {ExportId} не доставлено", AuditEventType.ExportFailed, id);
        }
      }
      if (!http.Response.HasStarted && !ct.IsCancellationRequested) {
        http.Response.Clear(); http.Response.StatusCode = ex is AuditUnavailableException or OperationCommitUnknownException ? 503 : ex is UnauthorizedAccessException or AntiforgeryValidationException ? 403 : ex is KeyNotFoundException ? 404 : 409;
        http.Response.Headers.CacheControl = "no-store";
        await http.Response.WriteAsync(ex is EntityRuleException ? ex.Message : "Не удалось передать файл. Проверьте доступ и создайте новую выгрузку.", ct);
      } else http.Abort();
    }
  }
}
