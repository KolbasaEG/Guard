using System.Collections.Concurrent;

namespace Guard.Core.Services.Notifications;

public sealed class NotificationSignals(ILogger<NotificationSignals> logger) : INotificationSignals
{
  private readonly ConcurrentDictionary<Guid, (string UserId, Action Callback)> subscriptions = new();
  public IDisposable Subscribe(string userId, Action invalidate)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(userId); ArgumentNullException.ThrowIfNull(invalidate);
    var id = Guid.NewGuid(); subscriptions[id] = (userId, invalidate);
    return new Subscription(() => subscriptions.TryRemove(id, out _));
  }
  public void Invalidate(string userId)
  {
    foreach (var subscription in subscriptions.Values.Where(s => s.UserId == userId))
      try { subscription.Callback(); } catch (Exception ex) { logger.LogWarning(ex, "Обновление центра уведомлений"); }
  }
  private sealed class Subscription(Action release) : IDisposable
  {
    private Action? cleanup = release;
    public void Dispose() => Interlocked.Exchange(ref cleanup, null)?.Invoke();
  }
}
