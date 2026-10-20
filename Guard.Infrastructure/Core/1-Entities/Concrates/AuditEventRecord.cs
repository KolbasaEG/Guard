using Guard.Core.Enums;

namespace Guard.Core.Entities;

public sealed class AuditEventRecord
{
  public Guid EventId { get; set; }
  public DateTime OccurredAtUtc { get; set; }
  public int SchemaVersion { get; set; } = 1;
  public AuditEventType EventType { get; set; }
  public string? Actor { get; set; }
  public string ActorKind { get; set; } = "anonymous";
  public string Outcome { get; set; } = "success";
  public string? ClientIp { get; set; }
  public string? CorrelationId { get; set; }
  public string? ObjectType { get; set; }
  public string? ObjectId { get; set; }
  public string? Details { get; set; }
  public int Attempts { get; set; }
  public DateTime NextAttemptAtUtc { get; set; }
  public Guid? LeaseToken { get; set; }
  public DateTime? LeaseUntilUtc { get; set; }
  // Завершённая запись в TLS; подтверждения обработки получателем нет.
  public DateTime? SentAtUtc { get; set; }
  public string? LastError { get; set; }
  public string? ReasonCode { get; set; }
  public string? AttemptedAccount { get; set; }
  public bool DeliveryRequired { get; set; }
  public DateTime? QueuedAtUtc { get; set; }
  public Guid DestinationVersion { get; set; }
  public int PayloadBytes { get; set; }
}
