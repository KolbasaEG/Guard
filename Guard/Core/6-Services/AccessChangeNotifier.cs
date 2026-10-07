using System.Collections.Concurrent;
namespace Guard.Core.Services;

// Один экземпляр на процесс. В подписке хранятся только ID и callback сессии.
public sealed class AccessChangeNotifier(ILogger<AccessChangeNotifier> logger) : IAccessChangeNotifier
{
  private readonly ConcurrentDictionary<long, Subscription> subscriptions = new();
  private long sequence;
  public IDisposable Subscribe(string userId, Func<Task> refresh)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(userId);
    ArgumentNullException.ThrowIfNull(refresh);
    var id = Interlocked.Increment(ref sequence);
    var subscription = new Subscription(userId, refresh, () => subscriptions.TryRemove(id, out _));
    subscriptions[id] = subscription;
    return subscription;
  }
  public Task PublishAsync(IEnumerable<string> userIds)
  {
    var targets = userIds.ToHashSet(StringComparer.Ordinal);
    return Task.WhenAll(subscriptions.Values.Where(s => targets.Contains(s.UserId)).Select(NotifyAsync));
  }
  private async Task NotifyAsync(Subscription subscription)
  {
    try { await subscription.NotifyAsync(); }
    catch (Exception ex) { logger.LogWarning(ex, "Не удалось уведомить сессию об изменении доступа"); }
  }
  private sealed class Subscription(string userId, Func<Task> refresh, Action remove) : IDisposable
  {
    private Func<Task>? callback = refresh;
    public string UserId { get; } = userId;
    public Task NotifyAsync() => Volatile.Read(ref callback)?.Invoke() ?? Task.CompletedTask;
    public void Dispose() { if (Interlocked.Exchange(ref callback, null) != null) remove(); }
  }
}
