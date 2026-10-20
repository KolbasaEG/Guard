using Guard.Core.Enums;
using Guard.Core.Services;

namespace Guard.Core.Logging;

// Итог отказавшей операции записывается после освобождения её транзакции.
internal static class AuditOperation
{
  internal static async Task<T> RunAsync<T>(IAuditService audit, AuditEventType type, string objectType,
    string? objectId, Func<Task<T>> action)
  {
    try { return await action(); }
    catch (Exception error) when (error is not (AuditUnavailableException or OperationCommitUnknownException)) {
      using var finish = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      try {
        await audit.RecordAsync(AuditEventData.Create(type, AuditOperationContext.Current?.UserId) with {
          Outcome = error is OperationCanceledException ? "cancelled" : error is UnauthorizedAccessException ? "denied" : "failure",
          ReasonCode = error.GetType().Name, ObjectType = objectType, ObjectId = objectId
        }, finish.Token);
      }
      catch (Exception auditError) {
        new InternalLogWriter().Write($"AuditRejectedOperationFailed EventType={type} ErrorType={auditError.GetType().Name}");
      }
      throw;
    }
  }
  internal static Task RunAsync(IAuditService audit, AuditEventType type, string objectType, string? objectId, Func<Task> action) =>
    RunAsync(audit, type, objectType, objectId, async () => { await action(); return true; });
}
