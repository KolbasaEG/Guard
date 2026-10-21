using Guard.Core.Enums;
using Guard.Core.Services;
using Microsoft.Extensions.Hosting;

namespace Guard.Core.Logging;

public sealed class TechnicalLogWorker(TechnicalLogRuntime runtime, TechnicalLogBuffer buffer, TechnicalLogDelivery delivery,
  IServiceScopeFactory scopes, InternalLogWriter diagnostics) : BackgroundService
{
  private DateTime nextProbe, nextCleanup;
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
    try { do {
      try { await TickAsync(stoppingToken); }
      catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
      catch (Exception error) {
        // Техническая диагностика не должна останавливать host через BackgroundServiceExceptionBehavior.
        buffer.Worker("Worker:" + error.GetType().Name);
        diagnostics.Write($"TechnicalLogWorkerFailed ErrorType={error.GetType().Name}");
      }
    } while (await timer.WaitForNextTickAsync(stoppingToken)); }
    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
  }
  public async Task TickAsync(CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();
    buffer.ProbeLocal();
    runtime.Expire();
    foreach (var expired in runtime.PendingExpirations().Take(4)) {
      try {
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAuditService>().RecordAsync(
          AuditEventData.Create(AuditEventType.LoggingConfigurationChanged, null,
            details: $"Action=DiagnosticExpired; SessionId={expired.Id}") with { EventId = expired.Id,
            OccurredAtUtc = expired.ExpiresAtUtc, ActorKind = "system", ReasonCode = "DiagnosticExpired" }, ct);
        runtime.ConfirmExpiration(expired.Id);
      } catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
      catch (Exception error) { diagnostics.Write($"TechnicalDiagnosticExpiryAuditFailed ErrorType={error.GetType().Name}"); }
    }
    try {
      using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
      var batch = buffer.ReadBatch(500);
      if (batch.Count > 0) {
        await delivery.DeliverAsync(batch.Select(item => item.Record).ToArray(), timeout.Token);
        buffer.Acknowledge(batch);
      }
      if (runtime.UtcNow >= nextProbe) {
        await delivery.ProbeAsync(timeout.Token); buffer.DatabaseProbe(); nextProbe = runtime.UtcNow.AddSeconds(30);
      }
      buffer.Worker();
    } catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
    catch (Exception error) { buffer.Worker(error.GetType().Name); }
    if (runtime.UtcNow >= nextCleanup) {
      nextCleanup = runtime.UtcNow.AddMinutes(5);
      try {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await delivery.CleanupAsync(runtime.UtcNow.AddDays(-runtime.Read().Settings.RetentionDays), timeout.Token);
        buffer.Cleanup(null);
      } catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
      catch (Exception error) { buffer.Cleanup(error.GetType().Name); }
      // Сбой БД не останавливает независимую очистку технических файлов.
      try { CleanFiles(Path.GetDirectoryName(buffer.DirectoryPath)!, runtime.UtcNow.AddDays(-runtime.Read().Settings.RetentionDays)); }
      catch (Exception error) { buffer.Cleanup("Files:" + error.GetType().Name); }
    }
  }
  public static void CleanFiles(string directory, DateTime cutoff)
  {
    if (!Directory.Exists(directory)) return;
    foreach (var file in Directory.EnumerateFiles(directory)) {
      var name = Path.GetFileName(file);
      if ((name.StartsWith("app-fallback-", StringComparison.Ordinal) || name.StartsWith("startup-", StringComparison.Ordinal) ||
        name.StartsWith("serilog-internal-errors-", StringComparison.Ordinal)) && File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
    }
    // technical-buffer очищается только после подтверждения БД, audit-* сюда не входит.
  }
}
