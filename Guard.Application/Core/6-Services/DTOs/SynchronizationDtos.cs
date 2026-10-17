using Guard.Core.Entities;

namespace Guard.Core.Services.DTOs;

// A read snapshot. Credentials are deliberately absent from this contract.
public sealed record SynchronizationSettingsDto
{
  public Guid Version { get; init; }
  public DataManagementMode Mode { get; init; }
  public bool ScheduleEnabled { get; init; }
  public string ApiBaseUrl { get; init; } = "";
  public int ApiPageSize { get; init; } = 500;
  public long? ApiHeadSubdivisionId { get; init; }
  public string ApiUsername { get; init; } = "";
  public bool CompleteSnapshotGuaranteed { get; init; }
  public bool HasApiPassword { get; init; }
  public TimeOnly DailyTime { get; init; } = new(2, 0);
  public DateOnly? LastScheduledDate { get; init; }
  public DateTime? ClassifiersSynchronizedAt { get; init; }
  public DateTime? LastFullSynchronizedAt { get; init; }
  public DateTime? LastChangesSynchronizedAt { get; init; }
  public DateOnly? RecoveryRequiredFrom { get; init; }
}

public sealed record SynchronizationRunDto
{
  public Guid Id { get; init; }
  public Guid? PoolId { get; init; }
  public bool Scheduled { get; init; }
  public DateOnly? UpdatedSince { get; init; }
  public DateTime StartedAt { get; init; }
  public DateTime? QueuedAt { get; init; }
  public string? Phase { get; init; }
  public DateTime? CompletedAt { get; init; }
  public SynchronizationStatus Status { get; init; }
  public int Added { get; init; }
  public int Updated { get; init; }
  public int Archived { get; init; }
  public int Restored { get; init; }
  public int Deactivated { get; init; }
  public int ExitEvents { get; init; }
  public int RepeatedExitEvents { get; init; }
  public int UnknownExitEmployees { get; init; }
  public string? Error { get; init; }
}
