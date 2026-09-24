using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System.Linq.Dynamic.Core;

namespace Guard.Components.Pages.Administrator.Classifiers
{
  public partial class Index : IDisposable
  {
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected ILogger<Index> Logger { get; set; } = default!;
    [Inject] protected IClassifierService ClassifierService { get; set; } = default!;

    [CascadingParameter] protected UserContext? UserContext { get; set; }

    int count;
    protected bool isEditor = true;
    protected bool isLoading = false;
    string pagingSummaryFormat = "Страница {0} из {1} (всего {2} записей)";

    private readonly CancellationTokenSource _cts = new();
    private CancellationTokenSource? _loadDataCts;

    protected IEnumerable<Classifier> data = default!;
    protected IEnumerable<Classifier> filteredData = default!;
    protected RadzenDataGrid<Classifier> grid = default!;
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

        var (items, totalCount) = await ClassifierService.QueryClassifiersAsync(async query =>
        {
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
            query = query.OrderBy(c => c.Type).ThenBy(c => c.Code);
          }

          var total = await query.CountAsync(ct);

          var pageData = await query
              .Skip(args.Skip ?? 0)
              .Take(args.Top ?? 10)
              .ToListAsync(ct);

          return (pageData, total);
        }, ct);

        filteredData = items;
        count = totalCount;
      }
      catch (OperationCanceledException)
      {
        // Игнорируем отмененные запросы
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка загрузки данных классификаторов");
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
        ShowSuccessNotification("Добавлена новая запись!");
        await grid.Reload();
      }
    }

    protected async Task EditRow(Classifier item)
    {
      var result = await DialogService.OpenAsync<Edit>("", new Dictionary<string, object?> { { "Id", item.Id } }, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });

      if (result != null)
      {
        ShowSuccessNotification("Информация обновлена!");
        await grid.Reload();
      }
    }

    protected async Task ToggleActiveStatusClick(MouseEventArgs args, Classifier item)
    {
      var actionText = item.IsActive ? "деактивировать" : "активировать";
      if (await DialogService.Confirm($"Вы действительно хотите {actionText} запись?", "Изменение статуса", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await ClassifierService.SetActiveStatusAsync(item.Id, !item.IsActive, _cts.Token);
          ShowSuccessNotification($"Запись успешно {(item.IsActive ? "деактивирована" : "активирована")}!");
          await grid.Reload();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
          Logger.LogError(ex, "Ошибка при изменении статуса классификатора ID: {ClassifierId}", item.Id);
          ShowErrorNotification(ex.Message);
        }
      }
    }

    protected async Task GridDeleteButtonClick(MouseEventArgs args, Classifier item)
    {
      if (await DialogService.Confirm("Вы действительно хотите полностью удалить запись?", "Удаление", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await ClassifierService.DeleteAsync(item.Id, _cts.Token);
          ShowSuccessNotification("Запись удалена!");
          await grid.Reload();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
          Logger.LogError(ex, "Ошибка при удалении классификатора ID: {ClassifierId}", item.Id);
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