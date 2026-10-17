using System.Reflection;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

internal static class WorkerLifecycleTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var scopes = new RejectingScopeFactory();
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    using var account = new AccountPolicyWorker(scopes, NullLogger<AccountPolicyWorker>.Instance);
    using var synchronization = new ApiSynchronizationWorker(scopes, NullLogger<ApiSynchronizationWorker>.Instance);
    using var export = new ExportWorker(scopes, NullLogger<ExportWorker>.Instance);
    foreach (var worker in new BackgroundService[] { account, synchronization, export }) {
      var execute = worker.GetType().GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
      var task = (Task)execute.Invoke(worker, [cancelled.Token])!;
      using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      try { await task.WaitAsync(timeout.Token); }
      catch (OperationCanceledException) when (task.IsCanceled) { }
      check(task.IsCompleted && scopes.Calls == 0,
        $"Cancelled {worker.GetType().Name} exits without opening an operation scope or database");
    }

    // The policy timer must stop without waiting for its first one-minute tick or invoking a sweep.
    using var running = new AccountPolicyWorker(scopes, NullLogger<AccountPolicyWorker>.Instance);
    await running.StartAsync(CancellationToken.None);
    using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await running.StopAsync(stopTimeout.Token);
    check(running.ExecuteTask?.IsCompleted == true && scopes.Calls == 0,
      "Account policy hosted service stops before its first tick without creating an operation scope");
  }

  private sealed class RejectingScopeFactory : IServiceScopeFactory
  {
    public int Calls { get; private set; }
    public IServiceScope CreateScope()
    {
      Calls++;
      throw new InvalidOperationException("An operation scope was opened after cancellation or before the first timer tick.");
    }
  }
}
