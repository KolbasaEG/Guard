using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Guard.Core.Logging;

namespace Guard.Core.Services.Exports;

public sealed class ExportDeliveryService(IPermissionService permissions, ExportAccess access, ExportStorage storage,
  IDbContextFactory<ApplicationDbContext> contexts, IAuditService audit, ILoggerFactory logs,
  IClientIpAccessor? clientIp = null, IOperationAvailability? availability = null) : IExportDeliveryService
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
        await db.SaveChangesAsync(ct);
        await AuditTransaction.RecordAsync(db, audit, AuditEventData.Create(AuditEventType.ExportDeliveryStarted, author,
          clientIp?.GetClientIp(), $"bytes={file.Size}; rows={job.RowCount}; SHA256={file.Sha256}")
          with { ObjectType = "Export", ObjectId = id.ToString() }, ct);
        await AuditTransaction.CommitAsync(transaction, ct); claimed = true;
      }
      if (availability != null) await availability.RequireWorkingAsync(ct);
      await using var stream = storage.Open(file.StorageKey);
      await using var guarded = new AuditGuardedStream(stream, availability);
      await deliver(new(file.FileName, file.ContentType, file.Size), guarded, ct);
      succeeded = true;
    }
    catch (OperationCommitUnknownException) { throw; }
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
          await AuditTransaction.RecordAsync(db, audit, AuditEventData.Create(succeeded ? AuditEventType.ExportDeliveryCompleted : AuditEventType.ExportFailed,
            author, clientIp?.GetClientIp()) with { ObjectType = "Export", ObjectId = id.ToString() }, finish.Token);
          await AuditTransaction.CommitAsync(transaction, finish.Token);
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

internal sealed class AuditGuardedStream(Stream source, IOperationAvailability? availability) : Stream
{
  public override bool CanRead => source.CanRead;
  public override bool CanSeek => false;
  public override bool CanWrite => false;
  public override long Length => source.Length;
  public override long Position { get => source.Position; set => throw new NotSupportedException(); }
  public override int Read(byte[] buffer, int offset, int count) {
    availability?.RequireWorkingAsync().GetAwaiter().GetResult(); return source.Read(buffer, offset, count);
  }
  public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) {
    if (availability != null) await availability.RequireWorkingAsync(ct);
    return await source.ReadAsync(buffer, ct);
  }
  public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
  public override void Flush() { }
  public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
  public override void SetLength(long value) => throw new NotSupportedException();
  public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
