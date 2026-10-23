using Guard.Core.Enums;

namespace Guard.Core.Services;

public sealed record UserNotificationDto(Guid Id, UserNotificationKind Kind, UserNotificationSeverity Severity,
  string Title, string Message, NotificationTarget Target, Guid? TargetId, bool IncidentActive,
  bool IsIncident, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, long Revision, bool IsRead);
public sealed record NotificationSearch(bool UnreadOnly = false, int Skip = 0, int Take = 20, long AfterRevision = 0, long ThroughRevision = long.MaxValue);
public sealed record NotificationPage(IReadOnlyList<UserNotificationDto> Items, int Count);
public sealed record NotificationSummary(int UnreadCount, long Watermark, IReadOnlyList<UserNotificationDto> Latest, int TotalCount = 0);

public interface IUserNotificationService
{
  Task<NotificationSummary> GetSummaryAsync(CancellationToken ct = default);
  Task<NotificationPage> SearchAsync(NotificationSearch query, CancellationToken ct = default);
  Task<UserNotificationDto?> GetAsync(Guid id, CancellationToken ct = default);
  Task<UserNotificationDto> GetAccessibleTargetAsync(Guid id, CancellationToken ct = default);
  Task MarkReadAsync(Guid id, long revision, CancellationToken ct = default);
  Task MarkAllReadAsync(long watermark, CancellationToken ct = default);
}

// Сигнал не содержит текстов и не даёт доступа к данным. Подписка живёт не дольше circuit.
public interface INotificationSignals
{
  IDisposable Subscribe(string userId, Action invalidate);
  void Invalidate(string userId);
}
