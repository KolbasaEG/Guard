using Guard.Core.Services.Exports;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using Radzen;
using Radzen.Blazor;


namespace Guard.Components.Pages.IpAddresses
{
  public partial class Index : IDisposable
  {

    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected ILogger<Index> Logger { get; set; } = default!;
    [Inject] protected IIpAddressManagementService IpAddressService { get; set; } = default!;

    [CascadingParameter] protected CurrentUserDisplayDto? UserContext { get; set; }

    int count;
    [CascadingParameter] public Guard.Core.Services.DTOs.UserAccessSnapshot? AccessSnapshot { get; set; }
    protected bool isEditor => AccessSnapshot?.Has(Permissions.IpAddresses.Manage) == true;
    protected bool isLoading = false;
    private bool allRecords;
    protected bool ShowAll => AccessSnapshot?.IsRoot == true && allRecords;
    private UserAccessSnapshot? loadedAccess;
    protected DataViewMode currentMode = DataViewMode.Active;
    protected SubdivisionHierarchyMode hierarchyMode = SubdivisionHierarchyMode.CurrentOnly;
    string pagingSummaryFormat = "Страница {0} из {1} (всего {2} записей)";

    private CancellationTokenSource _cts = new();
    private CancellationTokenSource? _loadDataCts;

    protected IEnumerable<IpAddressDto> filteredData = [];
    protected RadzenDataGrid<IpAddressDto> grid = default!;
    protected RadzenDataFilter<IpAddress> dataFilter = default!;
    

    IEnumerable<string>? itemsSubdivision;
    IEnumerable<string>? selectedItemsSubdivision;
    private ExportFilter? appliedFilter;
    private ExportFilter? exportFilter;
    private string? exportSort;
    [Inject] protected Guard.Components.Library.BrowserTimeService ExportTime { get; set; } = default!;
    protected async Task<ExportRequest> BuildExportRequestAsync(ExportFormat format)
    {
      var mode = currentMode;
      var includeChildren = hierarchyMode == SubdivisionHierarchyMode.IncludeChildren;
      var all = ShowAll;
      var order = exportSort;
      var filter = exportFilter;
      await ExportTime.InitializeAsync();
      return new(ExportArea.IpAddresses, format, mode, includeChildren,
        AllSubdivisions: all, OrderBy: order, Filter: filter, TimeZone: ExportTime.Zone!.Id);
    }
    private bool disposed;
    private string? accessVersion;
    private CurrentUserDisplayDto? loadedUserContext;
    private bool reloadPending = true;
    protected override void OnParametersSet()
    {
      if (AccessSnapshot?.IsRoot != true) allRecords = false;
      else if (loadedAccess?.IsRoot != true || loadedAccess.UserId != AccessSnapshot.UserId) allRecords = true;
      loadedAccess = AccessSnapshot;
      var version = AccessSnapshot == null ? "" : AccessSnapshot.UserId + ":" + AccessSnapshot.IsRoot + ":" + AccessSnapshot.Has(Permissions.IpAddresses.Read);
      if (accessVersion != null && version != accessVersion) {
        _loadDataCts?.Cancel(); filteredData = []; count = 0; reloadPending = true;
      }
      if (!ReferenceEquals(loadedUserContext, UserContext)) {
        loadedUserContext = UserContext;
        itemsSubdivision = UserContext?.SubordinateSubdivisions.Select(p => p.Name).Distinct().OrderBy(p => p).ToArray() ?? [];
        _loadDataCts?.Cancel(); filteredData = []; count = 0; reloadPending = true;
      }
      accessVersion = version;
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
      if (disposed || !reloadPending || AccessSnapshot == null) return;
      reloadPending = false;
      await grid.Reload();
      if (!disposed) StateHasChanged();
    }

    void OnSelectedSubdivisionChange(object value)
    {
      if (selectedItemsSubdivision != null && !selectedItemsSubdivision.Any())
      {
        selectedItemsSubdivision = null;
      }
    }

    async Task LoadData(LoadDataArgs args)
    {
      if (disposed) return;
      _loadDataCts?.Cancel();
      var request = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
      _loadDataCts = request;
      var ct = request.Token;
      var filter = appliedFilter;
      var mode = currentMode;
      var hierarchy = hierarchyMode;
      var showAll = ShowAll;
      var skip = args.Skip ?? 0;
      var take = args.Top ?? 20;
      var orderBy = args.OrderBy; exportSort = orderBy;
      isLoading = true;
      try
      {
        var result = await IpAddressService.SearchAsync(new(mode, hierarchy, showAll, skip, take, orderBy, filter), ct);
        ct.ThrowIfCancellationRequested();
        if (disposed || !ReferenceEquals(_loadDataCts, request)) return;
        filteredData = result.Items; count = result.Count;
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
      catch (Exception ex)
      {
        if (!disposed && ReferenceEquals(_loadDataCts, request)) {
          filteredData = []; count = 0;
          Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Ошибка загрузки IP-адресов");
          ShowErrorNotification("Не удалось загрузить IP-адреса. Проверьте доступ и повторите попытку.");
        }
      }
      finally
      {
        if (ReferenceEquals(_loadDataCts, request)) { _loadDataCts = null; if (!disposed) isLoading = false; }
        request.Dispose();
      }
    }

    protected async Task ResetPageAsync()
    {
      if (disposed) return;
      if (grid.CurrentPage == 0) await grid.Reload();
      else await grid.FirstPage(true);
    }
    protected async Task ReloadAsync()
    {
      if (!disposed) await grid.Reload();
    }
    protected async Task AddClick(MouseEventArgs args)
    {
      if (!isEditor || disposed) return;
      var result = await DialogService.OpenAsync<Add>("", null, new DialogOptions() { Width = "640px", Style = "max-width:calc(100vw - 24px);", ShowTitle = false, ContentCssClass = "rz-p-1" });
      if (Guard.Components.Library.Dialogs.EntityDialogResult.IsSuccess(result) && !disposed)
      {
        ShowSuccessNotification("Добавлена новая запись!");
        await grid.Reload();
      }
    }

    protected async Task ViewRow(IpAddressDto item)
    {
      if (disposed || AccessSnapshot?.Has(Permissions.IpAddresses.Read) != true) return;
      await DialogService.OpenAsync<Edit>("", new Dictionary<string, object?> { { "Id", item.Id }, { "ReadOnly", true } },
        new DialogOptions() { Width = "640px", Style = "max-width:calc(100vw - 24px);", ShowTitle = false, ContentCssClass = "rz-p-1" });
    }

    protected async Task EditRow(IpAddressDto item)
    {
      if (!isEditor || disposed || (Status)item.Status is not (Status.Inserted or Status.Modified)) return;
      var result = await DialogService.OpenAsync<Edit>("", new Dictionary<string, object?> { { "Id", item.Id } }, new DialogOptions() { Width = "640px", Style = "max-width:calc(100vw - 24px);", ShowTitle = false, ContentCssClass = "rz-p-1" });

      if (Guard.Components.Library.Dialogs.EntityDialogResult.IsSuccess(result) && !disposed)
      {
        ShowSuccessNotification("Информация обновлена!");
        await grid.Reload();
      }
    }

    protected async Task GridArchiveButtonClick(MouseEventArgs args, IpAddressDto item)
    {
      if (!isEditor || disposed || (Status)item.Status is not (Status.Inserted or Status.Modified)) return;
      if (await DialogService.Confirm("Вы действительно хотите поместить запись в архив?", "Архивирование", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await IpAddressService.ArchiveAsync(item.Id, _cts.Token);
          if (disposed || _cts.IsCancellationRequested) return;
          ShowSuccessNotification("Запись помещена в архив!");
          await grid.Reload();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
          Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Ошибка при архивировании IP-адреса ID: {IpAddressId}", item.Id);
          ShowErrorNotification("Не удалось выполнить операцию. Проверьте данные и права доступа.");
        }
      }
    }

    protected async Task GridUnarchiveButtonClick(MouseEventArgs args, IpAddressDto item)
    {
      if (!isEditor || disposed || (Status)item.Status != Status.Archived) return;
      if (await DialogService.Confirm("Вы действительно хотите извлечь запись из архива?", "Извлечение из архива", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await IpAddressService.RestoreAsync(item.Id, _cts.Token);
          if (disposed || _cts.IsCancellationRequested) return;
          ShowSuccessNotification("Запись извлечена из архива!");
          await grid.Reload();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
          Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Ошибка при извлечении из архива IP-адреса ID: {IpAddressId}", item.Id);
          ShowErrorNotification("Не удалось выполнить операцию. Проверьте данные и права доступа.");
        }
      }
    }

    protected async Task GridDeleteButtonClick(MouseEventArgs args, IpAddressDto item)
    {
      if (!isEditor || disposed || (Status)item.Status is not (Status.Inserted or Status.Modified)) return;
      if (await DialogService.Confirm("Вы действительно хотите удалить запись?", "Удаление", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await IpAddressService.SoftDeleteAsync(item.Id, _cts.Token);
          if (disposed || _cts.IsCancellationRequested) return;
          ShowSuccessNotification("Запись удалена!");
          await grid.Reload();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
          Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Ошибка при удалении IP-адреса ID: {IpAddressId}", item.Id);
          ShowErrorNotification("Не удалось выполнить операцию. Проверьте данные и права доступа.");
        }
      }
    }

    async Task ApplyFilter()
    {
      try {
        var candidate = IpListQuery.Capture(dataFilter);
        appliedFilter = candidate; exportFilter = candidate;
        await ResetPageAsync();
      } catch (ArgumentException ex) {
        Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Некорректные условия фильтра IP");
        ShowErrorNotification("Проверьте условия фильтра.");
      }
    }
    private async Task OnHierarchyModeChanged(bool isToggled)
    {
      hierarchyMode = isToggled
          ? SubdivisionHierarchyMode.IncludeChildren
          : SubdivisionHierarchyMode.CurrentOnly;

      await ResetPageAsync();
    }

    private async Task OnAllRecordsChanged(bool value)
    {
      allRecords = AccessSnapshot?.IsRoot == true && value;
      await ResetPageAsync();
    }

    private void ShowSuccessNotification(string detail)
    {
      if (disposed) return;
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
      if (disposed) return;
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
      _cts.Cancel();
      _cts.Dispose();
    }
  }
}
