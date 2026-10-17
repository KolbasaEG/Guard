using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public sealed class ApiSynchronizationWorker(IServiceScopeFactory scopes, ILogger<ApiSynchronizationWorker> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        using var scope = scopes.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<ApiSynchronizationRunner>();
        await runner.RecoverInterruptedAsync(stoppingToken);
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var db = await factory.CreateDbContextAsync(stoppingToken);
        var queued = await db.SynchronizationRuns.AsNoTracking().Where(r => r.Status == SynchronizationStatus.Queued)
          .OrderBy(r => r.QueuedAt).FirstOrDefaultAsync(stoppingToken);
        if (queued != null)
        {
          await runner.RunAsync(queued.ActorId, queued.PoolId, false, stoppingToken, queued.UpdatedSince, queued.Id);
          continue;
        }
        var settings = await db.SynchronizationSettings.AsNoTracking().SingleAsync(s => s.Id == 1, stoppingToken);
        if (SynchronizationSchedule.IsDue(settings, DateTimeOffset.UtcNow))
        {
          var run = await runner.RunAsync("system:api-sync", null, true, stoppingToken);
          if (run.Status != SynchronizationStatus.Succeeded)
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
      catch (Exception ex) { logger.LogError(ex, "Не удалось проверить расписание синхронизации"); }
      await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
    }
  }
}
