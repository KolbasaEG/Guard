using Guard.Core.Enums;

namespace Guard.Core.Entities;

// Security archive is independent of entity lifecycle and must survive deletion of its author.
public sealed class ExportJob
{
  public Guid Id { get; set; } = Guid.CreateVersion7();
  public string AuthorId { get; set; } = "";
  public string AuthorName { get; set; } = "";
  public string? ClientIp { get; set; }
  public string SecurityStamp { get; set; } = "";
  public ExportArea Area { get; set; }
  public ExportFormat Format { get; set; }
  public ExportJobState State { get; set; } = ExportJobState.Queued;
  public string RequestJson { get; set; } = "";
  public int SchemaVersion { get; set; } = 1;
  public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
  public DateTimeOffset? StartedAtUtc { get; set; }
  public DateTimeOffset? CompletedAtUtc { get; set; }
  public DateTimeOffset ExpiresAtUtc { get; set; }
  public long RowCount { get; set; }
  public bool CancelRequested { get; set; }
  public string? Error { get; set; }
  public ExportArtifact? Artifact { get; set; }
  public ExportDeliveryAttempt? Delivery { get; set; }
}

public sealed class ExportArtifact
{
  public Guid JobId { get; set; }
  public ExportJob Job { get; set; } = null!;
  public string FileName { get; set; } = "";
  public string StorageKey { get; set; } = "";
  public string ContentType { get; set; } = "";
  public string Sha256 { get; set; } = "";
  public long Size { get; set; }
  public string SubdivisionIdsJson { get; set; } = "[]";
  public bool IncludesUnassigned { get; set; }
  public DateTimeOffset? DeletedAtUtc { get; set; }
}

public sealed class ExportDeliveryAttempt
{
  public Guid JobId { get; set; }
  public ExportJob Job { get; set; } = null!;
  public DateTimeOffset StartedAtUtc { get; set; }
  public DateTimeOffset? CompletedAtUtc { get; set; }
  public bool Succeeded { get; set; }
}

public sealed class ExportRetentionSettings
{
  public int Id { get; set; } = 1;
  public int Years { get; set; } = 3;
  public Guid Version { get; set; } = Guid.NewGuid();
  public string? ModifiedBy { get; set; }
  public DateTimeOffset? ModifiedAtUtc { get; set; }
}
