using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services.Exports;

public sealed class ExportDeliveryService(IPermissionService permissions, ExportAccess access, ExportStorage storage,
  IDbContextFactory<ApplicationDbContext> contexts, IAuditService audit, ILoggerFactory logs) : IExportDeliveryService
{
  public async Task DeliverAsync(Guid id, Func<ExportDeliveryFile, Stream, CancellationToken, Task> deliver,
    CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(deliver);
    var logger = logs.CreateLogger("Guard.Core.Services.Exports.Delivery");
    bool locked = false, claimed = false, succeeded = false; string? author = null;
    try {
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
      await using var stream = storage.Open(file.StorageKey);
      await deliver(new(file.FileName, file.ContentType, file.Size), stream, ct);
      succeeded = true;
    }
    catch (Exception ex) {
      DeliverAudit(ex is UnauthorizedAccessException ? AuditEventType.AccessDenied : AuditEventType.ExportFailed);
      throw;
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
