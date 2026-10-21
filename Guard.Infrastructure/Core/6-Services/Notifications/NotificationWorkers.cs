using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Logging;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services.Notifications;

public sealed class NotificationDeliveryWorker(NotificationDispatcher dispatcher, ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var cleanupAt = DateTime.MinValue;
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
    try {
      do {
        try {
          await dispatcher.DeliverAsync(stoppingToken);
          if (DateTime.UtcNow >= cleanupAt) { await dispatcher.CleanupAsync(DateTime.UtcNow, stoppingToken); cleanupAt = DateTime.UtcNow.AddDays(1); }
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        catch (Exception ex) { logger.LogWarning(ex, "Доставка уведомлений отложена; повтор через 2 секунды"); }
      } while (await timer.WaitForNextTickAsync(stoppingToken));
    } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
  }
}

// Отдельный таймер обнаруживает остановку самого TechnicalLogWorker.
public sealed class NotificationHealthWorker(IDbContextFactory<ApplicationDbContext> factory, TechnicalLogBuffer buffer,
  ILogger<NotificationHealthWorker> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    string? lastStatus = null;
    try {
      await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
      using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
      do {
        try {
          var health = buffer.Health();
          if (health.Status != lastStatus) {
            await using var db = await factory.CreateDbContextAsync(stoppingToken);
            var ready = health.Status == "Ready";
            db.NotificationEvents.Add(new() {
              EventKey = "technical:" + Guid.CreateVersion7(), Kind = UserNotificationKind.TechnicalLogging,
              SourceKey = "technical-logging", Recovery = ready,
              Severity = ready ? UserNotificationSeverity.Success : health.Status == "Emergency" ? UserNotificationSeverity.Error : UserNotificationSeverity.Warning,
              Title = ready ? "Техническое логирование восстановлено" : "Сбой технического логирования",
              Message = ready ? "Запись и доставка технических событий работают." : "Нарушена работа технического журнала. Проверьте его состояние в разделе логов.",
              Target = NotificationTarget.TechnicalLogs, OccurredAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync(stoppingToken); lastStatus = health.Status;
          }
        } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        catch (Exception ex) { logger.LogWarning(ex, "Проверка инцидентов уведомлений отложена"); }
      } while (await timer.WaitForNextTickAsync(stoppingToken));
    } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
  }
}
