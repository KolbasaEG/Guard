using Guard.Core.Services;

namespace Guard.Web.Notifications;

// Экземпляр на circuit. Фоновая доставка сообщает только об изменении данных.
public sealed class NotificationCenterState(IUserNotificationService service, IPermissionService permissions,
  INotificationSignals signals, ILogger<NotificationCenterState> logger) : IAsyncDisposable
{
  public NotificationSummary? Summary { get; private set; }
  public string? Error { get; private set; }
  public event Action? Changed;
  public event Action<UserNotificationDto>? Arrived;
  private readonly CancellationTokenSource stopping = new();
  private readonly SemaphoreSlim refresh = new(1, 1);
  private readonly SemaphoreSlim wake = new(0, 1);
  private Task? polling;
  private IDisposable? subscription;
  private string? userId;
  private long? watermark;
  private bool disposed;

  public Task StartAsync()
  {
    polling ??= PollAsync(); return Task.CompletedTask;
  }
  private void Invalidate()
  {
    if (disposed) return;
    try { wake.Release(); } catch (SemaphoreFullException) { }
  }
  private async Task PollAsync()
  {
    try {
      do { await RefreshAsync(); }
      while (await wake.WaitAsync(TimeSpan.FromSeconds(30), stopping.Token) || !stopping.IsCancellationRequested);
    } catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
  }
  public async Task RefreshAsync()
  {
    var ct = stopping.Token;
    await refresh.WaitAsync(ct);
    try {
      if (disposed) return;
      var actor = await permissions.GetCurrentAsync(ct);
      if (userId != actor.UserId) {
        subscription?.Dispose(); userId = actor.UserId; watermark = null;
        subscription = signals.Subscribe(actor.UserId, Invalidate);
      }
      var result = await service.GetSummaryAsync(ct);
      var arrivals = new List<UserNotificationDto>();
      if (watermark.HasValue && result.Watermark > watermark.Value) {
        var skip = 0;
        NotificationPage page;
        do {
          page = await service.SearchAsync(new(Skip: skip, Take: 100, AfterRevision: watermark.Value, ThroughRevision: result.Watermark), ct);
          arrivals.AddRange(page.Items.Where(n => !n.IsRead)); skip += page.Items.Count;
        } while (page.Items.Count > 0 && skip < page.Count);
      }
      if (disposed) return;
      Summary = result; Error = null;
      // До callbacks: повторная перерисовка или сигнал не воспроизводят ту же версию.
      watermark = Math.Max(watermark ?? 0, result.Watermark);
      foreach (var item in arrivals.OrderBy(n => n.Revision)) Arrived?.Invoke(item);
    } catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex) {
      Summary = null; Error = "Уведомления временно недоступны.";
      logger.LogWarning(ex, "Загрузка центра уведомлений");
    } finally { refresh.Release(); if (!disposed) Changed?.Invoke(); }
  }
  public async Task MarkAllReadAsync()
  {
    if (Summary is { } snapshot) await service.MarkAllReadAsync(snapshot.Watermark, stopping.Token);
    await RefreshAsync();
  }
  public async ValueTask DisposeAsync()
  {
    if (disposed) return; disposed = true; subscription?.Dispose();
    await stopping.CancelAsync(); if (polling != null) await polling;
    stopping.Dispose();
  }
}
