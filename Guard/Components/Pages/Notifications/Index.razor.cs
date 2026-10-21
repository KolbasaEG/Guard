using Guard.Core.Services;
using Radzen;
using Radzen.Blazor;

namespace Guard.Components.Pages.Notifications;

public partial class Index
{
  private RadzenDataGrid<UserNotificationDto> grid = default!;
  private IReadOnlyList<UserNotificationDto> items = [];
  private int count, generation;
  private bool loading, unread, disposed;
  private string? error;
  private CancellationTokenSource? load;
  protected override void OnInitialized() => Center.Changed += Changed;
  private void Changed() => _ = InvokeAsync(async () => { if (!disposed && grid != null) await grid.Reload(); });
  private async Task LoadData(LoadDataArgs args)
  {
    load?.Cancel(); load?.Dispose(); load = new(); var ct = load.Token; var current = ++generation;
    loading = true;
    try {
      var page = await Service.SearchAsync(new(unread, args.Skip ?? 0, args.Top ?? 20), ct);
      if (disposed || current != generation) return;
      items = page.Items; count = page.Count; error = null;
    } catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex) {
      if (disposed || current != generation) return;
      items = []; count = 0; error = "Не удалось загрузить уведомления. Попробуйте обновить список.";
      Logger.LogWarning(ex, "История уведомлений");
    } finally { if (!disposed && current == generation) loading = false; }
  }
  private async Task FilterAsync(bool value) { unread = value; await grid.FirstPage(true); }
  private async Task ReloadAsync() => await grid.Reload();
  private async Task ReadAllAsync()
  {
    try { await Center.MarkAllReadAsync(); }
    catch (Exception ex) { error = "Не удалось отметить уведомления прочитанными."; Logger.LogWarning(ex, "Прочтение уведомлений"); }
  }
  private Task OpenAsync(UserNotificationDto item) => Dialogs.OpenAsync<Details>("", new Dictionary<string, object?> { ["Id"] = item.Id },
    new DialogOptions { Width = "700px", ShowTitle = false, ContentCssClass = "rz-p-1" });
  public void Dispose() { disposed = true; Center.Changed -= Changed; load?.Cancel(); load?.Dispose(); }
}
