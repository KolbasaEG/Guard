using Guard.Core.Enums;
using Guard.Core.Services.Exports;

namespace Guard.Core.Services;

public sealed record AuditSearchRequest(int Skip = 0, int Take = 20, string? OrderBy = null,
  string DeliveryState = "all", ExportFilter? Filter = null);
public sealed record AuditEntryDto(Guid EventId, DateTime OccurredAtUtc, AuditEventType EventType, string? Actor,
  string ActorKind, string Outcome, string? ClientIp, string? CorrelationId, string? ObjectType, string? ObjectId,
  int Attempts, DateTime? SentAtUtc, DateTime NextAttemptAtUtc, string? LastError,
  bool DeliveryRequired = true, string? ReasonCode = null, string? AttemptedAccount = null);
public sealed record AuditDetailsDto(AuditEntryDto Entry, string? Details, int SchemaVersion,
  IReadOnlyList<AuditDeliveryAttemptDto>? DeliveryAttempts = null);
public sealed record AuditSearchResult(IReadOnlyList<AuditEntryDto> Items, int Count);
public sealed record AuditQueueHealthDto(int Pending, int Retrying, DateTime? OldestPendingUtc,
  DateTime? LastSentUtc, long StorageFailures);

public interface IAuditSearchService
{
  Task<AuditSearchResult> SearchAsync(AuditSearchRequest request, CancellationToken ct = default);
  Task<AuditDetailsDto?> GetAsync(Guid eventId, CancellationToken ct = default);
  Task<AuditQueueHealthDto> GetHealthAsync(CancellationToken ct = default);
}
