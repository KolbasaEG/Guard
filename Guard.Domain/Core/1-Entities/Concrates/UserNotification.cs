using Guard.Core.Enums;

namespace Guard.Core.Entities;

public sealed class UserNotification
{
  public Guid Id { get; set; } = Guid.CreateVersion7();
  public string UserId { get; set; } = "";
  public string EventKey { get; set; } = "";
  public UserNotificationKind Kind { get; set; }
  public UserNotificationSeverity Severity { get; set; }
  public string Title { get; set; } = "";
  public string Message { get; set; } = "";
  public NotificationTarget Target { get; set; }
  public Guid? TargetId { get; set; }
  public bool RootOnly { get; set; }
  public Guid? IncidentId { get; set; }
  public bool IncidentActive { get; set; }
  public DateTime CreatedAtUtc { get; set; }
  public DateTime UpdatedAtUtc { get; set; }
  public long Revision { get; set; }
  public long ReadRevision { get; set; }
  public DateTime? ReadAtUtc { get; set; }

}

public sealed class NotificationIncident
{
  public Guid Id { get; set; } = Guid.CreateVersion7();
  public string SourceKey { get; set; } = "";
  public UserNotificationKind Kind { get; set; }
  public bool Active { get; set; } = true;
  public DateTime FirstSeenUtc { get; set; }
  public DateTime LastSeenUtc { get; set; }
  public DateTime? ResolvedAtUtc { get; set; }
  public int Occurrences { get; set; } = 1;
  public DateOnly? RequiredSince { get; set; }
  public bool FullSnapshotRequired { get; set; }
  public bool Covers(DateOnly? since) => !since.HasValue || !FullSnapshotRequired && RequiredSince.HasValue && since.Value <= RequiredSince.Value;
}

// Транзакционное событие. Обработанные ключи сохраняются для защиты от повторной доставки.
public sealed class NotificationEvent
{
  public Guid Id { get; set; } = Guid.CreateVersion7();
  public string EventKey { get; set; } = "";
  public string? UserId { get; set; }
  public UserNotificationKind Kind { get; set; }
  public UserNotificationSeverity Severity { get; set; }
  public string Title { get; set; } = "";
  public string Message { get; set; } = "";
  public NotificationTarget Target { get; set; }
  public Guid? TargetId { get; set; }
  public string? SourceKey { get; set; }
  public bool Recovery { get; set; }
  public DateOnly? UpdatedSince { get; set; }
  public DateTime OccurredAtUtc { get; set; }
  public DateTime? ProcessedAtUtc { get; set; }
}
