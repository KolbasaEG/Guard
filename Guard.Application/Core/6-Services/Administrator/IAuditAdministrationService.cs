using Guard.Core.Enums;

namespace Guard.Core.Services;

public sealed class AuditConfiguration
{
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
}

public sealed record AuditSystemState(AuditMode Mode, bool Available, string Code, string Message,
  DateTime CheckedAtUtc, long Pending, long PendingBytes, DateTime? OldestQueuedAtUtc,
  DateTime? LastSentUtc, DateTime? WorkerCompletedAtUtc, DateTime? LastStorageSuccessUtc,
  DateTime? LastFailureUtc, string? LastFailure, long StorageFailures,
  DateTime? LastCleanupUtc, string? CleanupError, DateTime? CertificateExpiresAtUtc = null,
  int MaxQueueAgeMinutes = 0, long MaxQueueBytes = 0, int MaxQueueCount = 0, Guid ConfigurationVersion = default);
public sealed record AuditDeliveryAttemptDto(Guid Id, DateTime StartedAtUtc, DateTime? CompletedAtUtc,
  string Destination, bool? Succeeded, string? Error);
public sealed record AuditConnectionTest(bool Succeeded, string Message, DateTime CheckedAtUtc, DateTime? CertificateExpiresAtUtc);

public sealed class AuditUnavailableException(string code, string message, Exception? inner = null)
  : InvalidOperationException(message, inner)
{
  public string Code { get; } = code;
}

public sealed class OperationCommitUnknownException(string? correlationId, Exception inner)
  : InvalidOperationException("Подтверждение сохранения потеряно. Изменение могло сохраниться. Обновите состояние перед повторением операции.", inner)
{
  public string? CorrelationId { get; } = correlationId;
}

public interface IOperationAvailability
{
  Task RequireWorkingAsync(CancellationToken ct = default);
}

public interface IAuditAdministrationService
{
  Task RequireRootAsync(CancellationToken ct = default);
  Task<AuditConfiguration> GetConfigurationAsync(CancellationToken ct = default);
  Task<AuditSystemState> GetStateAsync(CancellationToken ct = default);
  Task SaveAsync(AuditConfiguration configuration, bool transferPending, CancellationToken ct = default);
  Task<AuditConnectionTest> TestConnectionAsync(AuditConfiguration configuration, CancellationToken ct = default);
}
