using Guard.Core.Contexts;
using Guard.Core.Identity;
using Guard.Core.Logging;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services.Exports;

public sealed class ExportWorker(IServiceScopeFactory scopes, ILogger<ExportWorker> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var recovered = false; ApplicationDbContext? lease = null; DateTimeOffset cleanupAt = DateTimeOffset.MinValue, orphanScanAt = DateTimeOffset.MinValue;
    async Task ReleaseLeaseAsync() {
      if (lease == null) return;
      try {
        using var release = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        if (lease.Database.GetDbConnection().State == System.Data.ConnectionState.Open)
          await lease.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({PostgresAdvisoryLocks.ExportQueue})", release.Token);
      } catch (Exception ex) { logger.LogWarning(ex, "Освобождение блокировки очереди экспорта"); }
      finally { await lease.DisposeAsync(); lease = null; }
    }
    try {
    while (!stoppingToken.IsCancellationRequested) {
      try {
        await using var scope = scopes.CreateAsyncScope();
        if (scope.ServiceProvider.GetService<AuditJournal>() is { } journal && !(await journal.GetStateAsync(stoppingToken)).Available) {
          await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
          continue;
        }
        var processor = scope.ServiceProvider.GetRequiredService<ExportProcessor>();
        // Protect recovery from overlapping IIS restarts or a second process on the same server.
        if (lease == null) {
          var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
          var candidate = await factory.CreateDbContextAsync(stoppingToken);
          try {
            await candidate.Database.OpenConnectionAsync(stoppingToken);
            if (await candidate.Database.SqlQueryRaw<bool>($"SELECT pg_try_advisory_lock({PostgresAdvisoryLocks.ExportQueue}) AS \"Value\"").SingleAsync(stoppingToken)) lease = candidate;
            else await candidate.DisposeAsync();
          } catch { await candidate.DisposeAsync(); throw; }
        }
        if (lease == null) { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); continue; }
        await lease.Database.ExecuteSqlRawAsync("SELECT 1", stoppingToken);
        if (!recovered) { await processor.RecoverAsync(stoppingToken); recovered = true; }
        if (cleanupAt <= DateTimeOffset.UtcNow) {
          var scan = orphanScanAt <= DateTimeOffset.UtcNow;
          await processor.CleanupAsync(stoppingToken, scan); cleanupAt = DateTimeOffset.UtcNow.AddSeconds(30);
          if (scan) orphanScanAt = DateTimeOffset.UtcNow.AddDays(1);
        }
        if (await processor.RunNextAsync(stoppingToken)) continue;
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
      catch (Exception ex) {
        logger.LogError(ex, "Обработка очереди экспорта");
        await ReleaseLeaseAsync(); recovered = false;
      }
      try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); } catch (OperationCanceledException) { break; }
    }
    } finally { await ReleaseLeaseAsync(); }
  }
}
