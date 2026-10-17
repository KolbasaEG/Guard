using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services.Exports;

public static class ExportEndpoints
{
  public static IEndpointConventionBuilder MapExportEndpoints(this IEndpointRouteBuilder routes) =>
    routes.MapPost("/exports/{id:guid}/download", DownloadAsync).RequireAuthorization();

  private static async Task DownloadAsync(Guid id, HttpContext http, IAntiforgery antiforgery,
    IPermissionService permissions, ExportAccess access, ExportStorage storage, IDbContextFactory<ApplicationDbContext> contexts,
    IAuditService audit, ILoggerFactory logs)
  {
    var ct = http.RequestAborted; var logger = logs.CreateLogger("Guard.Core.Services.Exports.Delivery");
    bool locked = false, claimed = false, succeeded = false; string? author = null;
    try {
      await antiforgery.ValidateRequestAsync(http);
      if (http.Request.Headers.ContainsKey("Range")) throw new ArgumentException("Частичное скачивание не поддерживается.");
      var actor = await permissions.GetCurrentAsync(ct); author = actor.UserId;
      await storage.ArchiveGate.WaitAsync(ct); locked = true;
      await using var db = await contexts.CreateDbContextAsync(ct);
      var job = await db.ExportJobs.AsNoTracking().Include(j => j.Artifact).SingleOrDefaultAsync(j => j.Id == id && j.AuthorId == actor.UserId, ct)
        ?? throw new KeyNotFoundException("Выгрузка не найдена.");
      if (job.State != ExportJobState.Ready || job.CancelRequested || job.ExpiresAtUtc <= DateTimeOffset.UtcNow || job.Artifact is not { DeletedAtUtc: null } file)
        throw new EntityRuleException("Выгрузка недоступна или уже передавалась. Создайте новую выгрузку.");
      var request = JsonSerializer.Deserialize<ExportRequest>(job.RequestJson) ?? throw new InvalidDataException("Запрос выгрузки повреждён.");
      var subdivisions = JsonSerializer.Deserialize<Guid[]>(file.SubdivisionIdsJson) ?? throw new InvalidDataException("Область выгрузки повреждена.");
      await storage.VerifyAsync(file.StorageKey, file.Sha256, file.Size, ct);
      ExportAccess.CheckDelivery(await access.ForUserAsync(actor.UserId, request, ct), job, subdivisions, file.IncludesUnassigned);
      await using (var transaction = await db.Database.BeginTransactionAsync(ct)) {
        if (await db.ExportJobs.Where(j => j.Id == id && j.State == ExportJobState.Ready && !j.CancelRequested && j.ExpiresAtUtc > DateTimeOffset.UtcNow)
          .ExecuteUpdateAsync(u => u.SetProperty(j => j.State, ExportJobState.Delivering), ct) != 1)
          throw new EntityRuleException("Передача уже началась или выгрузка отменена.");
        db.ExportDeliveryAttempts.Add(new() { JobId = id, StartedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); claimed = true;
      }
      DeliverAudit(AuditEventType.ExportDeliveryStarted, job.ClientIp);
      http.Response.ContentType = file.ContentType; http.Response.ContentLength = file.Size;
      http.Response.Headers.CacheControl = "no-store, private";
      http.Response.Headers["X-Content-Type-Options"] = "nosniff";
      http.Response.Headers.ContentDisposition = "attachment; filename*=UTF-8''" + Uri.EscapeDataString(file.FileName);
      await using var stream = storage.Open(file.StorageKey); await stream.CopyToAsync(http.Response.Body, ct);
      await http.Response.Body.FlushAsync(ct); succeeded = true;
    }
    catch (Exception ex) {
      logger.LogWarning(ex, "Передача выгрузки {ExportId} не завершена", id);
      DeliverAudit(ex is UnauthorizedAccessException ? AuditEventType.AccessDenied : AuditEventType.ExportFailed);
      if (!http.Response.HasStarted && !ct.IsCancellationRequested) {
        http.Response.Clear(); http.Response.StatusCode = ex is UnauthorizedAccessException or AntiforgeryValidationException ? 403 : ex is KeyNotFoundException ? 404 : 409;
        http.Response.Headers.CacheControl = "no-store";
        await http.Response.WriteAsync(ex is EntityRuleException ? ex.Message : "Не удалось передать файл. Проверьте доступ и создайте новую выгрузку.", ct);
      } else if (!succeeded) http.Abort();
    }
    finally {
      try {
        if (claimed) {
          // Request cancellation must not prevent recording a consumed download attempt.
          using var finish = new CancellationTokenSource(TimeSpan.FromSeconds(15));
          await using var db = await contexts.CreateDbContextAsync(finish.Token);
          await using var transaction = await db.Database.BeginTransactionAsync(finish.Token);
          await db.ExportDeliveryAttempts.Where(d => d.JobId == id).ExecuteUpdateAsync(u => u.SetProperty(d => d.CompletedAtUtc, DateTimeOffset.UtcNow).SetProperty(d => d.Succeeded, succeeded), finish.Token);
          await db.ExportJobs.Where(j => j.Id == id && j.State == ExportJobState.Delivering).ExecuteUpdateAsync(u => u
            .SetProperty(j => j.State, succeeded ? ExportJobState.Delivered : ExportJobState.Failed)
            .SetProperty(j => j.Error, succeeded ? null : "Передача прервана. Создайте новую выгрузку."), finish.Token);
          await transaction.CommitAsync(finish.Token);
          DeliverAudit(succeeded ? AuditEventType.ExportDeliveryCompleted : AuditEventType.ExportFailed);
        }
      } catch (Exception ex) { logger.LogError(ex, "Не удалось записать результат передачи {ExportId}", id); }
      finally { if (locked) storage.ArchiveGate.Release(); }
    }

    void DeliverAudit(AuditEventType eventType, string? ip = null)
    {
      try { audit.LogIdentityEvent(eventType, author, ip, $"Export {id}"); }
      catch (Exception ex) {
        logger.LogError(ex, "Событие аудита {EventType} для передачи выгрузки {ExportId} не доставлено", eventType, id);
      }
    }
  }
}
