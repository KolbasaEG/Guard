using Guard.Components.Library;
using Guard.Core.Services.DTOs;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace Guard.Components.Pages.Classifiers
{
  public partial class Index : IDisposable
  {
    protected bool apiManaged = true;
    private readonly Guard.Components.Library.Loading.ListLoadConfirmation synchronizationLoad = new();
    protected Task<bool> ReloadForSynchronizationAsync() => synchronizationLoad.ReloadAsync(ReloadGridForSynchronizationAsync);
    protected virtual Task ReloadGridForSynchronizationAsync() { if (disposed || grid == null) return Task.CompletedTask; grid.ResetLoadData(); return grid.Reload(); }
    [Inject] protected IPermissionService PermissionService { get; set; } = default!;
    [Inject] protected BrowserTimeService Time { get; set; } = default!;
    private Guard.Core.Services.Exports.ExportFilter? appliedFilter;
    private bool disposed, reloadPending = true;
    private Guard.Core.Services.DTOs.UserAccessSnapshot? loadedAccess;
    protected override void OnParametersSet() { if (!ReferenceEquals(loadedAccess, Access)) { loadedAccess = Access; _loadDataCts?.Cancel(); filteredData = []; count = 0; reloadPending = true; } }
    protected override async Task OnAfterRenderAsync(bool firstRender) {
      if (disposed || !reloadPending) return; reloadPending = false;
      try { await Time.InitializeAsync(); await grid.Reload(); }
      catch (Exception ex) { Logger.LogWarning(ex, "Загрузка часового пояса"); ShowErrorNotification("Не удалось определить часовой пояс браузера. Обновите страницу."); }
      if (!disposed) StateHasChanged();
    }
    private async Task ResetPageAsync() { if (grid.CurrentPage == 0) await grid.Reload(); else await grid.FirstPage(true); }

    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected ILogger<Index> Logger { get; set; } = default!;
    [Inject] protected IClassifierService ClassifierService { get; set; } = default!;

    [CascadingParameter] protected UserContext? UserContext { get; set; }

    int count;
    [CascadingParameter] public Guard.Core.Services.DTOs.UserAccessSnapshot? Access { get; set; }
    protected bool isEditor => !apiManaged && Access?.Has(Guard.Core.Identity.Permissions.Classifiers.Manage) == true;
    protected bool isLoading = false;
    string pagingSummaryFormat = "Страница {0} из {1} (всего {2} записей)";

    private readonly CancellationTokenSource _cts = new();
    private CancellationTokenSource? _loadDataCts;

    protected IEnumerable<Classifier> data = [];
    protected IEnumerable<ClassifierListDto> filteredData = [];
    protected RadzenDataGrid<ClassifierListDto> grid = default!;
    protected RadzenDataFilter<Classifier> dataFilter = default!;

    protected override async Task OnInitializedAsync()
    {
      try
      {
        isLoading = true;
        Logger.LogDebug("Инициализация страницы классификаторов");
        await Task.CompletedTask;
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация страницы классификаторов была отменена");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации страницы классификаторов");
        ShowErrorNotification(Guard.Core.Services.UserOperationErrors.Message(ex));
      }
      finally
      {
        isLoading = false;
      }
    }

    protected async Task LoadData(LoadDataArgs args)
    {
      if (disposed || Time.Zone == null) return;
      var loadTicket = synchronizationLoad.Begin();
      _loadDataCts?.Cancel();
      var request = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
      _loadDataCts = request; var ct = request.Token;
      var filter = appliedFilter;
      isLoading = true;
      try {
        var result = await ClassifierService.SearchAsync(new(args.Skip ?? 0, args.Top ?? 20, args.OrderBy, filter), ct);
        ct.ThrowIfCancellationRequested();
        if (disposed || !ReferenceEquals(request, _loadDataCts)) return;
        filteredData = result.Items; count = result.Count; synchronizationLoad.Confirm(loadTicket);
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
      catch (Exception ex) {
        if (!disposed && ReferenceEquals(request, _loadDataCts)) {
          filteredData = []; count = 0; Logger.LogError(ex, "Загрузка списка");
          ShowErrorNotification("Не удалось загрузить данные. Проверьте доступ и повторите попытку.");
        }
      }
      finally { if (ReferenceEquals(request, _loadDataCts)) { isLoading = false; _loadDataCts = null; } request.Dispose(); }
    }
    protected async Task ReloadAsync()
    {
      await grid.Reload();
    }

    protected async Task AddClick(MouseEventArgs args)
    {
      var result = await DialogService.OpenAsync<Add>("", null, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
      if (Guard.Components.Library.Dialogs.EntityDialogResult.IsSuccess(result))
      {
        ShowSuccessNotification("Добавлена новая запись!");
        await grid.Reload();
      }
    }

    protected async Task EditRow(ClassifierListDto item)
    {
      var result = await DialogService.OpenAsync<Edit>("", new Dictionary<string, object?> { { "Id", item.Id } }, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });

      if (Guard.Components.Library.Dialogs.EntityDialogResult.IsSuccess(result))
      {
        ShowSuccessNotification("Информация обновлена!");
        await grid.Reload();
      }
    }

    protected async Task ToggleActiveStatusClick(MouseEventArgs args, ClassifierListDto item)
    {
      var actionText = item.IsActive ? "деактивировать" : "активировать";
      if (await DialogService.Confirm($"Вы действительно хотите {actionText} запись?", "Изменение статуса", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await ClassifierService.SetActiveStatusAsync(item.Id, !item.IsActive, _cts.Token, expectedVersion: item.Version);
          ShowSuccessNotification($"Запись успешно {(item.IsActive ? "деактивирована" : "активирована")}!");
          await grid.Reload();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
          Logger.LogError(ex, "Ошибка при изменении статуса классификатора ID: {ClassifierId}", item.Id);
          ShowErrorNotification(Guard.Core.Services.UserOperationErrors.Message(ex));
        }
      }
    }

    protected async Task GridDeleteButtonClick(MouseEventArgs args, ClassifierListDto item)
    {
      if (await DialogService.Confirm("Вы действительно хотите полностью удалить запись?", "Удаление", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await ClassifierService.DeleteAsync(item.Id, _cts.Token, expectedVersion: item.Version);
          ShowSuccessNotification("Запись удалена!");
          await grid.Reload();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
          Logger.LogError(ex, "Ошибка при удалении классификатора ID: {ClassifierId}", item.Id);
          ShowErrorNotification(Guard.Core.Services.UserOperationErrors.Message(ex));
        }
      }
    }

    async Task ApplyFilter() { try { appliedFilter = ClassifierListQuery.Capture(dataFilter, Time); await ResetPageAsync(); } catch (Exception ex) { Logger.LogWarning(ex, "Фильтр списка"); ShowErrorNotification(ex is ArgumentException ? ex.Message : "Не удалось применить фильтр."); } }

    private void ShowSuccessNotification(string detail)
    {
      NotificationService.Notify(new NotificationMessage
      {
        Severity = NotificationSeverity.Success,
        Summary = "Информационное",
        Detail = detail,
        Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
      });
    }

    private void ShowErrorNotification(string detail)
    {
      NotificationService.Notify(new NotificationMessage
      {
        Severity = NotificationSeverity.Error,
        Summary = "Внимание!",
        Detail = detail,
        Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
      });
    }

    public void Dispose()
    {
      disposed = true;
      _loadDataCts?.Cancel();

      _cts?.Cancel();
      _cts?.Dispose();
    }
  }
}
