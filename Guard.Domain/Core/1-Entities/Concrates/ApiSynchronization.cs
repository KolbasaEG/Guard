namespace Guard.Core.Entities;

public class SynchronizationSettings
{
  public int Id { get; set; } = 1;
  public Guid Version { get; set; } = Guid.NewGuid();
  public DataManagementMode Mode { get; set; }
  public bool ScheduleEnabled { get; set; }
  public string ApiBaseUrl { get; set; } = "";
  public int ApiPageSize { get; set; } = 500;
  public long? ApiHeadSubdivisionId { get; set; }
  public string ApiUsername { get; set; } = "";
  public bool CompleteSnapshotGuaranteed { get; set; }
  [System.Text.Json.Serialization.JsonIgnore]
  public string? ProtectedApiPassword { get; set; }
  [System.ComponentModel.DataAnnotations.Schema.NotMapped]
  public bool HasApiPassword { get; set; }
  public TimeOnly DailyTime { get; set; } = new(2, 0);
  public DateOnly? LastScheduledDate { get; set; }
  public DateTime? ClassifiersSynchronizedAt { get; set; }
  public DateTime? LastFullSynchronizedAt { get; set; }
  public DateTime? LastChangesSynchronizedAt { get; set; }
  public DateOnly? RecoveryRequiredFrom { get; set; }
  public string? ModifiedBy { get; set; }
  public DateTime? ModifiedAt { get; set; }
}

public class SynchronizationRun
{
  public Guid Id { get; set; } = Guid.CreateVersion7();
  public string ActorId { get; set; } = "system:api-sync";
  public Guid? PoolId { get; set; }
  public bool Scheduled { get; set; }
  public DateOnly? UpdatedSince { get; set; }
  public DateTime StartedAt { get; set; }
  public DateTime? QueuedAt { get; set; }
  public Guid? RequestedSettingsVersion { get; set; }
  public string? Phase { get; set; }
  public DateTime? CompletedAt { get; set; }
  public SynchronizationStatus Status { get; set; }
  public int Added { get; set; }
  public int Updated { get; set; }
  public int Archived { get; set; }
  public int Restored { get; set; }
  public int Deactivated { get; set; }
  public int ExitEvents { get; set; }
  public int RepeatedExitEvents { get; set; }
  public int UnknownExitEmployees { get; set; }
  public string? Error { get; set; }
}

public class ApiPersonnelExit
{
  public long PersonalExternalId { get; set; }
  public DateTime EventAt { get; set; }
  public DateTime ProcessedAt { get; set; }
}
