using Guard.Core.Enums;

namespace Guard.Core.Entities;

public sealed class AuditSettings
{
  public int Id { get; set; } = 1;
  public AuditMode Mode { get; set; }
  public bool Configured { get; set; }
  public int RetentionYears { get; set; } = 1;
  public int MaxQueueAgeMinutes { get; set; }
  public long MaxQueueBytes { get; set; }
  public int MaxQueueCount { get; set; }
  public string Host { get; set; } = "";
  public int Port { get; set; } = 6514;
  public string? CaCertificatePem { get; set; }
  public Guid Version { get; set; }
  public string? ModifiedBy { get; set; }
  public DateTime? ModifiedAtUtc { get; set; }
  public DateTime? WorkerCompletedAtUtc { get; set; }
}

public sealed class AuditDeliveryAttempt
{
  public Guid Id { get; set; }
  public Guid EventId { get; set; }
  public DateTime StartedAtUtc { get; set; }
  public DateTime? CompletedAtUtc { get; set; }
  public string Destination { get; set; } = "";
  public bool? Succeeded { get; set; }
  public string? Error { get; set; }
}
