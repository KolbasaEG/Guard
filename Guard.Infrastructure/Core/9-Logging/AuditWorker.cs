using Guard.Core.Enums;
using Guard.Core.Services;
using Microsoft.Extensions.Hosting;

namespace Guard.Core.Logging;

public sealed class AuditWorker(AuditDeliveryProcessor processor, LogRetention retention, InternalLogWriter diagnostics,
  AuditJournal journal) : BackgroundService
{
  protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(
    LoopAsync(() => processor.ProcessAsync(stoppingToken), TimeSpan.FromSeconds(2), "Delivery", stoppingToken),
    LoopAsync(() => retention.CleanAsync(stoppingToken), TimeSpan.FromMinutes(5), "Retention", stoppingToken),
    StateAsync(stoppingToken));

  private async Task LoopAsync(Func<Task> action, TimeSpan interval, string name, CancellationToken ct)
  {
    using var timer = new PeriodicTimer(interval); var failed = false;
    try {
      do {
        try { await action(); if (failed) diagnostics.Write($"Audit{name}Recovered"); failed = false; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        catch (Exception error) { if (!failed) diagnostics.Write($"Audit{name}Failed ErrorType={error.GetType().Name}"); failed = true; }
      } while (await timer.WaitForNextTickAsync(ct));
    } catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
  }

  private async Task StateAsync(CancellationToken ct)
  {
    string? previous = null;
    await LoopAsync(async () => {
      var state = await journal.GetStateAsync(ct); var code = state.Mode + ":" + state.Code;
      if (code == previous) return;
      diagnostics.Write($"AuditStateChanged State={code}");
      await journal.WriteAsync(AuditEventData.Create(AuditEventType.AuditStateChanged, null,
        details: $"Previous={previous}; Current={code}") with { Outcome = state.Available ? "success" : "failure", ReasonCode = state.Code }, ct);
      previous = code;
    }, TimeSpan.FromSeconds(5), "State", ct);
  }
}
