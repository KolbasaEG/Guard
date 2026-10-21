using System.Security.Cryptography;
using System.Text;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services.Notifications;

// Вызывается до SaveChanges в транзакции, которая сохраняет итог операции.
public static class NotificationOutbox
{
  public static string SynchronizationSource(SynchronizationSettings settings) => "sync:" +
    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new {
      Url = settings.ApiBaseUrl.TrimEnd('/'), settings.ApiHeadSubdivisionId, settings.ApiUsername
    }))));

  public static void Export(ApplicationDbContext db, ExportJob job, bool ready)
  {
    if (string.IsNullOrEmpty(job.AuthorId)) return;
    db.NotificationEvents.Add(new() {
      EventKey = $"export:{job.Id}:{(ready ? "ready" : "failed")}", UserId = job.AuthorId,
      Kind = ready ? UserNotificationKind.ExportReady : UserNotificationKind.ExportFailed,
      Severity = ready ? UserNotificationSeverity.Success : UserNotificationSeverity.Error,
      Title = ready ? "Выгрузка готова" : "Ошибка выгрузки",
      Message = ready ? "Документ сформирован. Откройте выгрузку для скачивания." : "Не удалось сформировать документ. Откройте выгрузку для просмотра результата.",
      Target = NotificationTarget.Export, TargetId = job.Id, OccurredAtUtc = DateTime.UtcNow
    });
  }

  public static void Synchronization(ApplicationDbContext db, SynchronizationRun run)
  {
    var success = run.Status == SynchronizationStatus.Succeeded;
    var failed = run.Status is SynchronizationStatus.Failed or SynchronizationStatus.Interrupted;
    if (run.Status is SynchronizationStatus.Queued or SynchronizationStatus.Running) return;
    var now = run.CompletedAt ?? DateTime.UtcNow;
    if (!run.Scheduled) db.NotificationEvents.Add(new() {
      EventKey = $"sync:{run.Id}:result", UserId = run.ActorId, Kind = UserNotificationKind.SynchronizationResult,
      Severity = success ? UserNotificationSeverity.Success : failed ? UserNotificationSeverity.Error : UserNotificationSeverity.Warning,
      Title = success ? "Синхронизация завершена" : "Синхронизация не завершена",
      Message = success ? $"Добавлено: {run.Added}, изменено: {run.Updated}, архивировано: {run.Archived}, восстановлено: {run.Restored}." :
        run.Status == SynchronizationStatus.Cancelled ? "Синхронизация отменена." : run.Status == SynchronizationStatus.Interrupted ?
        "Синхронизация прервана перезапуском сервера." : "Не удалось выполнить синхронизацию. Подробности доступны в разделе синхронизации.",
      Target = NotificationTarget.Synchronization, TargetId = run.Id, OccurredAtUtc = now
    });
    if (run.NotificationSourceKey != null && (run.Scheduled && failed || success && !run.PoolId.HasValue))
      db.NotificationEvents.Add(new() {
        EventKey = $"sync:{run.Id}:incident", Kind = UserNotificationKind.ScheduledSynchronization,
        SourceKey = run.NotificationSourceKey, Recovery = success, UpdatedSince = run.UpdatedSince,
        Severity = success ? UserNotificationSeverity.Success : UserNotificationSeverity.Error,
        Title = success ? "Синхронизация восстановлена" : "Сбой плановой синхронизации",
        Message = success ? "Данные источника успешно синхронизированы." : "Плановая синхронизация не завершена. Проверьте состояние источника и повторите запуск.",
        Target = NotificationTarget.Synchronization, TargetId = run.Id, OccurredAtUtc = now
      });
  }

  public static async Task RecoverSynchronizationAsync(ApplicationDbContext db, CancellationToken ct)
  {
    await using var transaction = await db.Database.BeginTransactionAsync(ct);
    var runs = await db.SynchronizationRuns.Where(r => r.Status == SynchronizationStatus.Running).ToListAsync(ct);
    foreach (var run in runs) {
      run.Status = SynchronizationStatus.Interrupted; run.CompletedAt = DateTime.UtcNow;
      run.Error = "Предыдущий процесс завершился до окончания синхронизации.";
      Synchronization(db, run);
    }
    await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
  }
}
