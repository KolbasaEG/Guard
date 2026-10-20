using System.Diagnostics.Metrics;

namespace Guard.Core.Logging;

public sealed class AuditHealth : IDisposable
{
  private readonly Meter meter = new("Guard.Audit", "1.0");
  private readonly Counter<long> stored;
  private readonly Counter<long> failures;
  private readonly Counter<long> sent;
  private long storageFailures;
  private long pending;
  private long oldestSeconds;
  private long queueUpdatedAt;
  public long StorageFailures => Interlocked.Read(ref storageFailures);
  private readonly object gate = new();
  public DateTime? LastStorageSuccessUtc { get; private set; }
  public DateTime? LastFailureUtc { get; private set; }
  public string? LastFailure { get; private set; }
  public DateTime? LastCleanupUtc { get; private set; }
  public string? CleanupError { get; private set; }
  public DateTime? CertificateExpiresAtUtc { get; private set; }

  public AuditHealth()
  {
    stored = meter.CreateCounter<long>("guard.audit.stored");
    failures = meter.CreateCounter<long>("guard.audit.storage_failures");
    sent = meter.CreateCounter<long>("guard.audit.sent");
    meter.CreateObservableGauge("guard.audit.pending", () => Interlocked.Read(ref pending));
    meter.CreateObservableGauge("guard.audit.oldest_pending_seconds", () => Interlocked.Read(ref oldestSeconds));
    meter.CreateObservableGauge("guard.audit.queue_updated_unix_seconds", () => Interlocked.Read(ref queueUpdatedAt));
  }
  public void Stored() { stored.Add(1); StorageChecked(); }
  public void StorageChecked() { lock (gate) LastStorageSuccessUtc = DateTime.UtcNow; }
  public void StoreFailed(string? error = null) {
    Interlocked.Increment(ref storageFailures); failures.Add(1);
    lock (gate) { LastFailureUtc = DateTime.UtcNow; LastFailure = error; }
  }
  public void Cleanup(string? error) { lock (gate) { LastCleanupUtc = DateTime.UtcNow; CleanupError = error; } }
  public void Certificate(DateTime? expires) { lock (gate) CertificateExpiresAtUtc = expires; }
  public void Sent() => sent.Add(1);
  public void Queue(int count, DateTime? oldest)
  {
    Interlocked.Exchange(ref pending, count);
    Interlocked.Exchange(ref oldestSeconds, oldest.HasValue ? Math.Max(0, (long)(DateTime.UtcNow - oldest.Value).TotalSeconds) : 0);
    Interlocked.Exchange(ref queueUpdatedAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
  }
  public void Dispose() => meter.Dispose();
}
