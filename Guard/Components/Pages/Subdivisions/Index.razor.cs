using Guard.Core.Services.Exports;
using Guard.Components.Library;
using Guard.Core.Services.DTOs;
using Guard.Components.Library.Loading;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Enums;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace Guard.Components.Pages.Subdivisions
{
  public partial class Index : IDisposable
  {
    protected async Task<ExportRequest> BuildExportRequestAsync(ExportFormat format)
    {
      await Time.InitializeAsync();
      return new(ExportArea.Subdivisions, format, currentMode, hierarchyMode == SubdivisionHierarchyMode.IncludeChildren,
        ShowAll, exportSort, appliedFilter, TimeZone: Time.Zone!.Id);
    }
    protected bool apiManaged = true;
    private readonly Guard.Components.Library.Loading.ListLoadConfirmation synchronizationLoad = new();
    protected Task<bool> ReloadForSynchronizationAsync() => synchronizationLoad.ReloadAsync(ReloadGridForSynchronizationAsync);
    protected virtual Task ReloadGridForSynchronizationAsync() { if (disposed || grid == null) return Task.CompletedTask; grid.ResetLoadData(); return grid.Reload(); }
    protected bool allRecords;
    protected bool ShowAll => Access?.IsRoot == true && allRecords;
    [Inject] protected BrowserTimeService Time { get; set; } = default!;
    private ExportFilter? appliedFilter;
    private string? exportSort;
    private bool disposed, reloadPending = true;
    private Guard.Core.Services.DTOs.CurrentUserDisplayDto? loadedContext;
    private Guard.Core.Services.DTOs.UserAccessSnapshot? loadedAccess;
    protected override void OnParametersSet() { if (!ReferenceEquals(loadedAccess, Access) || !ReferenceEquals(loadedContext, UserContext)) {
      if (Access?.IsRoot != true) allRecords = false;
      else if (loadedAccess?.IsRoot != true || loadedAccess.UserId != Access.UserId) allRecords = true;
      loadedAccess = Access; loadedContext = UserContext; _loadDataCts?.Cancel(); filteredData = []; count = 0; reloadPending = true; } }
    protected async Task OnAllRecordsChanged(bool value) { allRecords = Access?.IsRoot == true && value; await ResetPageAsync(); }
    protected override async Task OnAfterRenderAsync(bool firstRender) {
      if (disposed || !reloadPending) return; reloadPending = false;
      try { await Time.InitializeAsync(); await grid.Reload(); }
      catch (Exception ex) { Logger.LogWarning(ex, "Загрузка часового пояса"); ShowErrorNotification("Не удалось определить часовой пояс браузера. Обновите страницу."); }
      if (!disposed) StateHasChanged();
    }
    private async Task ToggleHierarchyAsync() { hierarchyMode = hierarchyMode == SubdivisionHierarchyMode.CurrentOnly ? SubdivisionHierarchyMode.IncludeChildren : SubdivisionHierarchyMode.CurrentOnly; await ResetPageAsync(); }
    private async Task ResetPageAsync() { if (grid.CurrentPage == 0) await grid.Reload(); else await grid.FirstPage(true); }

    [Inject]
    protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject]
    protected NotificationService NotificationService { get; set; } = default!;
    [Inject]
    protected DialogService DialogService { get; set; } = default!;
    [Inject]
    protected ISubdivisionService SubdivisionService { get; set; } = default!;
    [Inject]
    protected ILogger<Index> Logger { get; set; } = default!;

    protected IEnumerable<SubdivisionListDto> filteredData = [];
    protected RadzenDataGrid<SubdivisionListDto> grid = default!;

    protected RadzenDataFilter<Subdivision> dataFilter = default!;

    private CancellationTokenSource _cts = new();
    private CancellationTokenSource? _loadDataCts;

    [CascadingParameter] protected Guard.Core.Services.DTOs.CurrentUserDisplayDto? UserContext { get; set; }
    protected SubdivisionHierarchyMode hierarchyMode = SubdivisionHierarchyMode.CurrentOnly;
    int count;
    [CascadingParameter] public Guard.Core.Services.DTOs.UserAccessSnapshot? Access { get; set; }
    protected bool isEditor => !apiManaged && Access?.Has(Guard.Core.Identity.Permissions.Subdivisions.Write) == true;
    protected bool isLoading = false;
    protected DataViewMode currentMode = DataViewMode.Active; 
    string pagingSummaryFormat = "Страница {0} из {1} (всего {2} записей)";

    protected override async Task OnInitializedAsync()
    {
      try
      {
        isLoading = true;
        await Task.CompletedTask;
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации страницы подразделений");
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
      exportSort = args.OrderBy;
      var loadTicket = synchronizationLoad.Begin();
      _loadDataCts?.Cancel();
      var request = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
      _loadDataCts = request; var ct = request.Token;
      var filter = appliedFilter;
      var mode = currentMode; var hierarchy = hierarchyMode; var showAll = ShowAll;
      isLoading = true;
      try {
        var result = await SubdivisionService.SearchAsync(new SubdivisionSearchRequest(
          mode, hierarchy, showAll, args.Skip ?? 0, args.Top ?? 20, args.OrderBy, filter), ct);
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
    protected async Task AddClick(MouseEventArgs args)
    {
      var result = await DialogService.OpenAsync<Add>("", null, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
      if (Guard.Components.Library.Dialogs.EntityDialogResult.IsSuccess(result))
      {
        ShowSuccessNotification("Добавлена новая запись!"); 
        await grid.Reload();
      }
    }
    protected async Task EditRow(SubdivisionListDto item)
    {
      var result = await DialogService.OpenAsync<Edit>("", new Dictionary<string, object?> { { "Id", item.Id } }, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
      if (Guard.Components.Library.Dialogs.EntityDialogResult.IsSuccess(result))
      {
        ShowSuccessNotification("Информация обновлена!");
        await grid.Reload();
      }
    }
    protected async Task GridMoveButtonClick(MouseEventArgs args, SubdivisionListDto item)
    {
      var result = await DialogService.OpenAsync<Move>("", new Dictionary<string, object?> { { "Id", item.Id } }, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
      if (Guard.Components.Library.Dialogs.EntityDialogResult.IsSuccess(result))
      {
        ShowSuccessNotification("Информация обновлена!");
        await grid.Reload();
      }
    }
    protected async Task GridArchiveButtonClick(MouseEventArgs args, SubdivisionListDto item)
    {
      if (await DialogService.Confirm("Вы действительно хотите поместить запись в архив?", "Архивирование", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await SubdivisionService.ArchiveAsync(item.Id, ct: _cts.Token, expectedVersion: item.Version);
          ShowSuccessNotification("Запись помещена в архив!");
          await grid.Reload();
        }
        catch (Exception ex)
        {
          ShowErrorNotification(Guard.Core.Services.UserOperationErrors.Message(ex));
        }
      }
    }
    private async Task ToggleBlockAsync(SubdivisionListDto item)
    {
      var unblock = EntityStatusTransitions.CanApply(item.Status, StatusOperation.Unblock);
      if (await DialogService.Confirm(unblock ? "Разблокировать выбранную запись?" : "Заблокировать выбранную запись?", "Изменение статуса",
        new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) != true) return;
      try {
        if (unblock) await SubdivisionService.UnblockAsync(item.Id, _cts.Token, item.Version);
        else await SubdivisionService.BlockAsync(item.Id, _cts.Token, item.Version);
        ShowSuccessNotification("Статус записи изменён."); await grid.Reload();
      }
      catch (Exception ex) { Logger.LogWarning(ex, "Блокировка подразделения"); ShowErrorNotification(UserOperationErrors.Message(ex)); }
    }
    protected async Task GridUnarchiveButtonClick(MouseEventArgs args, SubdivisionListDto item)
    {
      if (await DialogService.Confirm("Вы действительно хотите извлечь запись из архива?", "Извлечение из архива", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await SubdivisionService.RestoreAsync(item.Id, ct: _cts.Token, expectedVersion: item.Version);
          ShowSuccessNotification("Запись извлечена из архива!");
          await grid.Reload();
        }
        catch (Exception ex)
        {
          ShowErrorNotification(Guard.Core.Services.UserOperationErrors.Message(ex));
        }
      }
    }
    protected async Task GridDeleteButtonClick(MouseEventArgs args, SubdivisionListDto item)
    {
      if (await DialogService.Confirm("Вы действительно хотите удалить запись?", "Удаление", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await SubdivisionService.SoftDeleteAsync(item.Id, ct: _cts.Token, expectedVersion: item.Version);
          ShowSuccessNotification("Запись удалена!");
          await grid.Reload();
        }
        catch (Exception ex)
        {
          ShowErrorNotification(Guard.Core.Services.UserOperationErrors.Message(ex));
        }
      }
    }
    protected async Task ReloadAsunc()
    {
      await grid.Reload();
    }
    protected async Task RebuildHierarchyAndPathsAsync()
    {
      isLoading = true;

      try
      {
        Logger.LogInformation("Запуск операции пересчета иерархии и путей подразделений...");
        await SubdivisionService.RebuildHierarchyAndPathsAsync(_cts.Token);

        ShowSuccessNotification("Иерархия и пути подразделений обновлены.");
        await grid.Reload();
      }
      catch (OperationCanceledException)
      {
        Logger.LogWarning("Операция пересчета иерархии подразделений была отменена.");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при выполнении пересчета иерархии и путей подразделений.");
        ShowErrorNotification("Не удалось перестроить иерархию подразделений.");
      }
      finally
      {
        isLoading = false;
      }
    }


    async Task ApplyFilter()
    {
      try { appliedFilter = SubdivisionListQuery.Capture(dataFilter, Time); await ResetPageAsync(); }
      catch (Exception ex) { Logger.LogWarning(ex, "Фильтр списка"); ShowErrorNotification(ex is ArgumentException ? ex.Message : "Не удалось применить фильтр."); }
    }
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
      if (disposed) return;
      disposed = true;
      _loadDataCts?.Cancel();

      _cts?.Cancel();
      _cts?.Dispose();
    }

  }
}
