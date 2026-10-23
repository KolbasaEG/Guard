using Guard.Core.Services;
using Radzen;
using Radzen.Blazor;

namespace Guard.Components.Pages.Notifications;

public partial class Index
{
  private RadzenDataGrid<UserNotificationDto> grid = default!;
  private IReadOnlyList<UserNotificationDto> items = [];
  private int count, generation;
  private bool loading, unread, reading, disposed;
  private bool initialLoadPending = true;
  private bool backgroundRefreshPending, backgroundRefreshing;
  private bool nextPageLoadInBackground;
  private string? error;
  private CancellationTokenSource? load;
  protected override void OnInitialized() => Center.Changed += Changed;
  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (disposed || !initialLoadPending || grid == null) return;
    initialLoadPending = false;
    backgroundRefreshPending = false;
    await ReloadAsync();
  }
  private async void Changed()
  {
    try {
      await InvokeAsync(async () => {
        if (disposed) return;
        backgroundRefreshPending = true;
        await RefreshInBackgroundAsync();
      });
    }
    catch (Exception ex) { if (!disposed) await DispatchExceptionAsync(ex); }
  }
  private Task LoadData(LoadDataArgs args)
  {
    var background = nextPageLoadInBackground;
    nextPageLoadInBackground = false;
    return LoadPageAsync(args, background);
  }
  private async Task LoadPageAsync(LoadDataArgs args, bool background)
  {
    if (disposed) return;
    load?.Cancel(); load?.Dispose(); load = new(); var ct = load.Token; var current = ++generation;
    loading = !background;
    try {
      var page = await Service.SearchAsync(new(unread, args.Skip ?? 0, args.Top ?? 20), ct);
      ct.ThrowIfCancellationRequested();
      if (disposed || current != generation) return;
      if (background && (args.Skip ?? 0) > 0 && (args.Skip ?? 0) >= page.Count) {
        // Текущая страница исчезла, например после прочтения всех записей фильтра.
        // Однократный флаг забирает LoadData до ожидания ответа сервиса.
        nextPageLoadInBackground = true;
        try { await grid.GoToPage(Math.Max(0, (page.Count - 1) / grid.PageSize), true); }
        finally { nextPageLoadInBackground = false; }
        return;
      }
      if (error != null || count != page.Count || !items.SequenceEqual(page.Items)) {
        items = page.Items; count = page.Count; error = null;
        if (background) StateHasChanged();
      }
    } catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex) {
      if (disposed || current != generation) return;
      items = []; count = 0; error = "Не удалось загрузить уведомления. Попробуйте обновить список.";
      Logger.LogWarning(ex, "История уведомлений");
      if (background) StateHasChanged();
    } finally {
      if (!disposed && current == generation) {
        loading = false;
        if (!background) {
          if (backgroundRefreshPending) StateHasChanged();
          await RefreshInBackgroundAsync();
        }
      }
    }
  }
  private async Task RefreshInBackgroundAsync()
  {
    if (disposed || initialLoadPending || grid == null || loading || backgroundRefreshing) return;
    backgroundRefreshing = true;
    try {
      while (!disposed && backgroundRefreshPending && !loading) {
        backgroundRefreshPending = false;
        await LoadPageAsync(new LoadDataArgs { Skip = grid.CurrentPage * grid.PageSize, Top = grid.PageSize }, true);
      }
    }
    finally { backgroundRefreshing = false; }
  }
  private async Task FilterAsync(bool value) { unread = value; await grid.FirstPage(true); }
  private async Task ReloadAsync()
  {
    if (disposed || grid == null) return;
    await grid.Reload();
    if (!disposed) StateHasChanged();
  }
  private async Task ReadAllAsync()
  {
    if (reading || disposed || Center.Summary == null) return;
    reading = true;
    try {
      await Center.MarkAllReadAsync();
      Notify(NotificationSeverity.Success, "Уведомления отмечены прочитанными.");
    }
    catch (Exception ex) {
      Notify(NotificationSeverity.Error, "Не удалось отметить уведомления прочитанными. Повторите попытку.");
      Logger.LogWarning(ex, "Прочтение уведомлений");
    }
    finally { reading = false; }
  }
  private void Notify(NotificationSeverity severity, string detail)
  {
    if (disposed) return;
    Notifications.Notify(new NotificationMessage {
      Severity = severity, Summary = "Уведомления", Detail = detail,
      Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
    });
  }
  private Task OpenAsync(UserNotificationDto item) => Dialogs.OpenAsync<Details>("", new Dictionary<string, object?> { ["Id"] = item.Id },
    new DialogOptions { Width = "700px", ShowTitle = false, ContentCssClass = "guard-entity-dialog-content" });
  public void Dispose() { disposed = true; Center.Changed -= Changed; load?.Cancel(); load?.Dispose(); }
}
