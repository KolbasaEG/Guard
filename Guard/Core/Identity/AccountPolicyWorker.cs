using Guard.Core.Services;

namespace Guard.Core.Identity;

public sealed class AccountPolicyWorker(IServiceScopeFactory scopes, ILogger<AccountPolicyWorker> logger) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
    while (await timer.WaitForNextTickAsync(stoppingToken))
    {
      try {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAccountPolicyService>().SweepAsync(stoppingToken);
      }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
      catch (Exception ex) { logger.LogError(ex, "Проверка политики учётных записей"); }
    }
  }
}
