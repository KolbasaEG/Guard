using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Extensions;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System.Linq.Dynamic.Core;

namespace Guard.Components.Pages.Administrator.MaintenanceSectors
{
  public partial class Index : IDisposable
  {
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected ILogger<Index> Logger { get; set; } = default!;
    [Inject] protected IMaintenanceSectorService SectorService { get; set; } = default!;

    [CascadingParameter] protected UserContext? UserContext { get; set; }

    int count;
    protected bool isEditor = true;
    protected bool isLoading = false;
    protected string subdivisionPath = "-";
    protected DataViewMode currentMode = DataViewMode.Active;
    protected SubdivisionHierarchyMode hierarchyMode = SubdivisionHierarchyMode.CurrentOnly;
    string pagingSummaryFormat = "Страница {0} из {1} (всего {2} записей)";

    private CancellationTokenSource _cts = new();
    private CancellationTokenSource? _loadDataCts;

    protected IEnumerable<MaintenanceSector> data = default!;
    protected IEnumerable<MaintenanceSector> filteredData = default!;
    protected RadzenDataGrid<MaintenanceSector> grid = default!;
    protected RadzenDataFilter<MaintenanceSector> dataFilter = default!;

    IEnumerable<string>? itemsSubdivision;
    IEnumerable<string>? selectedItemsSubdivision;
    IEnumerable<string>? finalSelectedItemsSubdivision;

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
        Logger.LogDebug("Инициализация страницы участков обслуживания");
        itemsSubdivision = UserContext?.SubordinateSubdivisions.Select(p => p.Name) ?? [];
        subdivisionPath = UserContext?.Subdivision?.Path ?? "-";
        await Task.CompletedTask;
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация страницы участков обслуживания была отменена");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации страницы участков обслуживания");
        ShowErrorNotification(ex.Message);
      }
      finally
      {
        isLoading = false;
      }
    }

    async Task LoadData(LoadDataArgs args)
    {
      _loadDataCts?.Cancel();
      _loadDataCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
      var ct = _loadDataCts.Token;
      isLoading = true;

      try
      {
        if (dataFilter?.Filters != null) NormalizeFilterDatesToUtc(dataFilter.Filters);

        var (items, totalCount) = await SectorService.QuerySectorsAsync(async query =>
        {
          query = query
            .FilterByMode(currentMode)
            .FilterBySubdivision(subdivisionPath, hierarchyMode)
            .Include(s => s.Subdivision)
            .Include(s => s.ResponsiblePersonal);

          if (dataFilter != null)
          {
            query = query.Where(dataFilter);
          }

          if (!string.IsNullOrEmpty(args.OrderBy))
          {
            query = query.OrderBy(args.OrderBy);
          }
          else
          {
            query = query.OrderByDescending(s => s.InsertedDate);
          }

          var total = await query.CountAsync(ct);

          var pageData = await query
              .Skip(args.Skip ?? 0)
              .Take(args.Top ?? 10)
              .ToListAsync(ct);

          return (pageData, total);
        }, ct);

        filteredData = items.ConvertDateTimesToLocal();
        count = totalCount;
      }
      catch (OperationCanceledException)
      {
        // Игнорируем отмененные запросы
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка загрузки данных участков обслуживания");
        ShowErrorNotification("Не удалось загрузить данные");
      }
      finally
      {
        isLoading = false;
      }
    }

    protected async Task ReloadAsync()
    {
      await grid.Reload();
    }

    protected async Task AddClick(MouseEventArgs args)
    {
      var result = await DialogService.OpenAsync<Add>("", null, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
      if (result != null)
      {
        ShowSuccessNotification("Добавлен новый участок!");
        await grid.Reload();
      }
    }

    protected async Task EditRow(MaintenanceSector item)
    {
      var result = await DialogService.OpenAsync<Edit>("", new Dictionary<string, object?> { { "Id", item.Id } }, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });

      if (result != null)
      {
        ShowSuccessNotification("Информация об участке обновлена!");
        await grid.Reload();
      }
    }

    protected async Task GridArchiveButtonClick(MouseEventArgs args, MaintenanceSector item)
    {
      if (await DialogService.Confirm("Вы действительно хотите поместить участок в архив?", "Архивирование", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await SectorService.ArchiveAsync(item.Id, _cts.Token);
          ShowSuccessNotification("Участок помещен в архив!");
          await grid.Reload();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
          Logger.LogError(ex, "Ошибка при архивировании участка ID: {SectorId}", item.Id);
          ShowErrorNotification(ex.Message);
        }
      }
    }

    protected async Task GridUnarchiveButtonClick(MouseEventArgs args, MaintenanceSector item)
    {
      if (await DialogService.Confirm("Вы действительно хотите извлечь участок из архива?", "Извлечение из архива", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await SectorService.RestoreAsync(item.Id, _cts.Token);
          ShowSuccessNotification("Участок извлечен из архива!");
          await grid.Reload();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
          Logger.LogError(ex, "Ошибка при извлечении из архива участка ID: {SectorId}", item.Id);
          ShowErrorNotification(ex.Message);
        }
      }
    }

    protected async Task GridDeleteButtonClick(MouseEventArgs args, MaintenanceSector item)
    {
      if (await DialogService.Confirm("Вы действительно хотите удалить участок?", "Удаление", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await SectorService.SoftDeleteAsync(item.Id, _cts.Token);
          ShowSuccessNotification("Участок удален!");
          await grid.Reload();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
          Logger.LogError(ex, "Ошибка при удалении участка ID: {SectorId}", item.Id);
          ShowErrorNotification(ex.Message);
        }
      }
    }

    private async Task OnExportClick()
    {
      // TODO: Реализовать экспорт
      await Task.CompletedTask;
    }

    async Task ApplyFilter()
    {
      finalSelectedItemsSubdivision = selectedItemsSubdivision;
      await grid.Reload();
    }

    private async Task OnHierarchyModeChanged(bool isToggled)
    {
      hierarchyMode = isToggled
          ? SubdivisionHierarchyMode.IncludeChildren
          : SubdivisionHierarchyMode.CurrentOnly;

      await grid.Reload();
    }

    private void NormalizeFilterDatesToUtc(IEnumerable<CompositeFilterDescriptor> filters)
    {
      if (filters == null) return;

      foreach (var filter in filters)
      {
        if (filter.FilterValue is DateTime dt && dt.Kind != DateTimeKind.Utc)
        {
          filter.FilterValue = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        if (filter.Filters != null && filter.Filters.Any())
        {
          NormalizeFilterDatesToUtc(filter.Filters);
        }
      }
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
      _loadDataCts?.Cancel();
      _loadDataCts?.Dispose();
      _cts?.Cancel();
      _cts?.Dispose();
    }
  }
}