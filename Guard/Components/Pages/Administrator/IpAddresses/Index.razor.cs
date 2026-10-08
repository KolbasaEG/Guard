using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Extensions;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;

using Radzen;
using Radzen.Blazor;


namespace Guard.Components.Pages.Administrator.IpAddresses
{
  public partial class Index : IDisposable
  {

    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected ILogger<Index> Logger { get; set; } = default!;
    [Inject] protected IIpAddressService IpAddressService { get; set; } = default!;

    [CascadingParameter] protected UserContext? UserContext { get; set; }

    int count;
    [CascadingParameter] public Guard.Core.Services.DTOs.UserAccessSnapshot? AccessSnapshot { get; set; }
    protected bool isEditor => AccessSnapshot?.Has(Permissions.IpAddresses.Manage) == true;
    [Inject] protected IIpManagementAccessService Access { get; set; } = default!;
    protected bool isLoading = false;
    protected string subdivisionPath = "-";
    protected DataViewMode currentMode = DataViewMode.Active;
    protected SubdivisionHierarchyMode hierarchyMode = SubdivisionHierarchyMode.CurrentOnly;
    string pagingSummaryFormat = "Страница {0} из {1} (всего {2} записей)";

    private CancellationTokenSource _cts = new();
    private CancellationTokenSource? _loadDataCts;

    protected IEnumerable<IpAddress> data = [];
    protected IEnumerable<IpAddressDto> filteredData = [];
    protected RadzenDataGrid<IpAddressDto> grid = default!;
    protected RadzenDataFilter<IpAddress> dataFilter = default!;
    

    IEnumerable<string>? itemsSubdivision;
    IEnumerable<string>? selectedItemsSubdivision;
    private Func<IQueryable<IpAddress>, IQueryable<IpAddress>> appliedFilter = query => query;
    private bool disposed;
    protected string? loadError;
    private string? accessVersion;
    private UserContext? loadedUserContext;
    private bool reloadPending = true;
    protected override void OnParametersSet()
    {
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
      if (disposed || !reloadPending) return;
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

    protected override async Task OnInitializedAsync()
    {
      try
      {
        isLoading = true;
        Logger.LogDebug("Инициализация страницы IP-адресов");
        subdivisionPath = UserContext?.Subdivision?.Path ?? "-";
        await Task.CompletedTask;
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация страницы IP-адресов была отменена");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации страницы IP-адресов");
        ShowErrorNotification("Не удалось выполнить операцию. Проверьте данные и права доступа.");
      }
      finally
      {
        isLoading = false;
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
      var path = UserContext?.Subdivision?.Path;
      var skip = args.Skip ?? 0;
      var take = args.Top ?? 20;
      var orderBy = args.OrderBy;
      isLoading = true; loadError = null;
      try
      {
        if (skip < 0 || take is < 1 or > 100) throw new ArgumentException("Недопустимые параметры страницы.");
        var initialAccess = await Access.GetScopeAsync(false, ct);
        var (items, totalCount) = await IpAddressService.QueryIpAddressesAsync(async query =>
        {
          query = query.FilterByMode(mode);
          query = string.IsNullOrWhiteSpace(path) ? query.Where(p => false) : query.FilterBySubdivision(path, hierarchy);
          query = filter(query);
          var total = await query.CountAsync(ct);
          var page = await IpListQuery.Sort(query, orderBy).Skip(skip).Take(take)
            .Select(p => new IpAddressDto {
              Id = p.Id, Address = p.Address, Description = p.Description, Status = (int)p.Status,
              SubdivisionName = p.Subdivision != null ? p.Subdivision.Name : null
            }).ToListAsync(ct);
          return (page, total);
        }, ct);
        ct.ThrowIfCancellationRequested();
        // Повторная серверная проверка перед показом результата.
        var finalAccess = await Access.GetScopeAsync(false, ct);
        if (initialAccess.UserId != finalAccess.UserId || initialAccess.IsRoot != finalAccess.IsRoot ||
            !initialAccess.SubdivisionIds.SetEquals(finalAccess.SubdivisionIds))
          throw new UnauthorizedAccessException("Область доступа изменилась во время загрузки.");
        if (disposed || !ReferenceEquals(_loadDataCts, request)) return;
        filteredData = items; count = totalCount;
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
      catch (Exception ex)
      {
        if (!disposed && ReferenceEquals(_loadDataCts, request)) {
          filteredData = []; count = 0;
          loadError = "Не удалось загрузить IP-адреса. Проверьте доступ и повторите попытку.";
          Logger.LogError(ex, "Ошибка загрузки IP-адресов");
          ShowErrorNotification(loadError);
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
      if (!disposed) await grid.FirstPage(true);
    }
    protected async Task ReloadAsync()
    {
      if (!disposed) await grid.Reload();
    }
    protected async Task AddClick(MouseEventArgs args)
    {
      if (!isEditor || disposed) return;
      var result = await DialogService.OpenAsync<Add>("", null, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
      if (result is true && !disposed)
      {
        ShowSuccessNotification("Добавлена новая запись!");
        await grid.Reload();
      }
    }

    protected async Task EditRow(IpAddressDto item)
    {
      if (!isEditor || disposed || (Status)item.Status is not (Status.Inserted or Status.Modified)) return;
      var result = await DialogService.OpenAsync<Edit>("", new Dictionary<string, object?> { { "Id", item.Id } }, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });

      if (result is true && !disposed)
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
          ShowSuccessNotification("Запись помещена в архив!");
          await grid.Reload();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
          Logger.LogError(ex, "Ошибка при архивировании IP-адреса ID: {IpAddressId}", item.Id);
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
          ShowSuccessNotification("Запись извлечена из архива!");
          await grid.Reload();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
          Logger.LogError(ex, "Ошибка при извлечении из архива IP-адреса ID: {IpAddressId}", item.Id);
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
          ShowSuccessNotification("Запись удалена!");
          await grid.Reload();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
          Logger.LogError(ex, "Ошибка при удалении IP-адреса ID: {IpAddressId}", item.Id);
          ShowErrorNotification("Не удалось выполнить операцию. Проверьте данные и права доступа.");
        }
      }
    }

    async Task ApplyFilter()
    {
      try {
        appliedFilter = IpListQuery.Capture(dataFilter);
        await ResetPageAsync();
      } catch (ArgumentException ex) {
        Logger.LogWarning(ex, "Некорректные условия фильтра IP");
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
