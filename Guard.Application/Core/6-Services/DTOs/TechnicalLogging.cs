namespace Guard.Core.Services.DTOs;

public sealed record TechnicalLogSettings
{
  public ApplicationLogLevel NormalLevel { get; init; } = ApplicationLogLevel.Information;
  public int RetentionDays { get; init; } = 30;
  public int BufferMegabytes { get; init; } = 3072;
  public int MinimumFreeMegabytes { get; init; } = 1024;
  public int DefaultDiagnosticMinutes { get; init; } = 15;
  public int MaximumDiagnosticMinutes { get; init; } = 60;
  public int ErrorThreshold { get; init; } = 20;
  public int ErrorWindowMinutes { get; init; } = 5;

  public void Validate()
  {
    if (NormalLevel is < ApplicationLogLevel.Information or > ApplicationLogLevel.Error ||
      RetentionDays is < 1 or > 3650 || BufferMegabytes is < 16 or > 10240 ||
      MinimumFreeMegabytes is < 128 or > 1048576 || DefaultDiagnosticMinutes < 1 ||
      MaximumDiagnosticMinutes is < 1 or > 1440 || DefaultDiagnosticMinutes > MaximumDiagnosticMinutes ||
      ErrorThreshold is < 1 or > 100000 || ErrorWindowMinutes is < 1 or > 60)
      throw new ArgumentException("Проверьте уровень, сроки, лимиты хранения и порог ошибок.");
  }
}

public sealed record DiagnosticLogRequest(ApplicationLogLevel Level, int Minutes, string Category, string Reason);
public sealed record DiagnosticLogSession(Guid Id, string ActorId, ApplicationLogLevel Level, string Category,
  string Reason, DateTime StartedAtUtc, DateTime ExpiresAtUtc);
public sealed record TechnicalLogHealth(DateTime ObservedAtUtc, string Status, string? BufferError, string? DatabaseError,
  DateTime? LastLocalWriteUtc, DateTime? LastDatabaseWriteUtc, DateTime? LastDatabaseProbeUtc,
  DateTime? LastWorkerUtc, DateTime? LastCleanupUtc, string? CleanupError, long PendingCount, long PendingBytes,
  DateTime? OldestPendingUtc, long LostEvents, int ErrorsInWindow, bool ErrorStorm, long? FreeDiskBytes,
  string? ConfigurationError, long QuarantinedCount = 0, long QuarantinedBytes = 0,
  IReadOnlyList<string>? Problems = null, DateTime? FailureSinceUtc = null, DateTime? LastRecoveryUtc = null,
  DateTime? LastLocalProbeUtc = null);
public sealed record TechnicalLogState(Guid Version, TechnicalLogSettings Settings, DiagnosticLogSession? Session,
  TechnicalLogHealth Health, string InstanceId);
public sealed record TechnicalErrorGroup(string Fingerprint, string? Category, long Count, DateTime FirstUtc, DateTime LastUtc);
public enum TechnicalLogSource { Database, LocalBuffer }
public sealed record TechnicalLogExportRequest(DateTime FromUtc, DateTime ToUtc, string? CorrelationId = null,
  Guid? DiagnosticSessionId = null, TechnicalLogSource Source = TechnicalLogSource.Database, string? Level = null, string? AfterCursor = null);
public sealed record TechnicalLogExport(string FileName, byte[] Content, int Count, bool Truncated, Guid Id = default);
