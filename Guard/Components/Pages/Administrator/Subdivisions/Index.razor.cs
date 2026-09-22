using Guard.ComponentLibrary.Loading;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Extensions;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System.Linq.Dynamic.Core;

namespace Guard.Components.Pages.Administrator.Subdivisions
{
  public partial class Index : IDisposable
  {
    [Inject]
    protected IJSRuntime JSRuntime { get; set; }
    [Inject]
    protected NotificationService NotificationService { get; set; }
    [Inject]
    protected DialogService DialogService { get; set; }
    [Inject]
    protected ISubdivisionService SubdivisionService { get; set; }
    [Inject]
    protected ILogger<Index> Logger { get; set; }

    protected IEnumerable<Subdivision> data; 
    protected IEnumerable<Subdivision> filteredData;
    protected RadzenDataGrid<Subdivision> grid;

    protected RadzenDataFilter<Subdivision> dataFilter;

    private CancellationTokenSource _cts = new();
    int count;
    protected bool isEditor = true;
    protected bool isLoading = false;
    protected DataViewMode currentMode = DataViewMode.Active; 
    string pagingSummaryFormat = "Страница {0} из {1} (всего {2} записей)";

    protected LoadingIndicator loading;

    protected override async Task OnInitializedAsync()
    {
      try
      {
        isLoading = true;
        Logger.LogDebug("");

        // Имитация загрузки (например, справочников)
        await Task.CompletedTask;
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "");

        NotificationService.Notify(new NotificationMessage
        {
          Severity = NotificationSeverity.Error,
          Summary = "Внимание!",
          Detail = ex.Message,
          Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
        });
      }
      finally
      {
        isLoading = false;
      }
    }

    async Task LoadData(LoadDataArgs args)
    {
      isLoading = true;

      try
      {
        if(dataFilter?.Filters != null) NormalizeFilterDatesToUtc(dataFilter.Filters);
        var (items, totalCount) = await SubdivisionService.QuerySubdivisionsAsync(async query =>
            {
              query = query.FilterByMode(currentMode);
              query = query.Include(p => p.Parent);
              query = query.Include(p => p.OrganType);
              query = query.Include(p => p.StatusClassifier);
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

              var total = await query.CountAsync();

              var pageData = await query
                  .Skip(args.Skip ?? 0)
                  .Take(args.Top ?? 10)
                  .ToListAsync();

              return (pageData, total);
            });

        filteredData = items.ConvertDateTimesToLocal();
        count = totalCount;
      }
      finally
      {
        isLoading = false;
      }
    }
    protected async Task AddClick(MouseEventArgs args)
    {
      var result = await DialogService.OpenAsync<Add>("", null, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
      if (result != null)
      {
        NotificationService.Notify(new NotificationMessage
        {
          Severity = NotificationSeverity.Success,
          Summary = $"Информационное",
          Detail = $"Добавлена новая запись!",
          Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
        });
        await grid.Reload();
      }
    }
    protected async Task EditRow(Subdivision item)
    {
      var result = await DialogService.OpenAsync<Edit>("", new Dictionary<string, object> { { "Id", item.Id } }, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
      if (result != null)
      {
        NotificationService.Notify(new NotificationMessage
        {
          Severity = NotificationSeverity.Success,
          Summary = $"Информационное",
          Detail = $"Информация обновлена!",
          Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
        });
        await grid.Reload();
      }
    }

    protected async Task GridMoveButtonClick(MouseEventArgs args, Subdivision item)
    {
      var result = await DialogService.OpenAsync<Move>("", new Dictionary<string, object> { { "Id", item.Id } }, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
      if (result != null)
      {
        NotificationService.Notify(new NotificationMessage
        {
          Severity = NotificationSeverity.Success,
          Summary = $"Информационное",
          Detail = $"Информация обновлена!",
          Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
        });
        await grid.Reload();
      }
    }

    protected async Task GridArchiveButtonClick(MouseEventArgs args, Subdivision item)
    {
      if (await DialogService.Confirm("Вы действительно хотите поместить запись в архив?", "Архивирование", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await SubdivisionService.ArchiveAsync(item.Id, ct: CancellationToken.None);
          NotificationService.Notify(new NotificationMessage
          {
            Severity = NotificationSeverity.Success,
            Summary = $"Информационное",
            Detail = $"Запись удалена!",
            Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
          });
          await grid.Reload();
        }
        catch (Exception ex)
        {
          NotificationService.Notify(new NotificationMessage
          {
            Severity = NotificationSeverity.Error,
            Summary = $"Внимание!",
            Detail = ex.Message,
            Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
          });
        }
      }
    }
    protected async Task GridUnarchiveButtonClick(MouseEventArgs args, Subdivision item)
    {
      if (await DialogService.Confirm("Вы действительно хотите извлечь запись из архива?", "Извлечение из архива", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await SubdivisionService.RestoreAsync(item.Id, ct: CancellationToken.None);
          NotificationService.Notify(new NotificationMessage
          {
            Severity = NotificationSeverity.Success,
            Summary = $"Информационное",
            Detail = $"Запись удалена!",
            Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
          });
          await grid.Reload();
        }
        catch (Exception ex)
        {
          NotificationService.Notify(new NotificationMessage
          {
            Severity = NotificationSeverity.Error,
            Summary = $"Внимание!",
            Detail = ex.Message,
            Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
          });
        }
      }
    }
    protected async Task GridDeleteButtonClick(MouseEventArgs args, Subdivision item)
    {
      if (await DialogService.Confirm("Вы действительно хотите удалить запись?", "Удаление", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await SubdivisionService.SoftDeleteAsync(item.Id, ct: CancellationToken.None);
          NotificationService.Notify(new NotificationMessage
          {
            Severity = NotificationSeverity.Success,
            Summary = $"Информационное",
            Detail = $"Запись удалена!",
            Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
          });
          await grid.Reload();
        }
        catch (Exception ex)
        {
          NotificationService.Notify(new NotificationMessage
          {
            Severity = NotificationSeverity.Error,
            Summary = $"Внимание!",
            Detail = ex.Message,
            Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
          });
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

        Logger.LogInformation("Иерархия и пути подразделений успешно перестроены.");

        NotificationService.Notify(new NotificationMessage
        {
          Severity = NotificationSeverity.Success,
          Summary = "Успешно",
          Detail = "Иерархия и пути подразделений обновлены.",
          Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
        });

        await grid.Reload();
      }
      catch (OperationCanceledException)
      {
        Logger.LogWarning("Операция пересчета иерархии подразделений была отменена.");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при выполнении пересчета иерархии и путей подразделений.");

        NotificationService.Notify(new NotificationMessage
        {
          Severity = NotificationSeverity.Error,
          Summary = "Ошибка",
          Detail = "Не удалось перестроить иерархию подразделений.",
          Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
        });
      }
      finally
      {
        isLoading = false;
      }
    }
    private async Task OnExportClick()
    {
    }
    async Task ApplyFilter()
    {

    }
    private void NormalizeFilterDatesToUtc(IEnumerable<CompositeFilterDescriptor> filters)
    {
      if (filters == null) return;

      foreach (var filter in filters)
      {
        if (filter.FilterValue is DateTime dt && dt.Kind != DateTimeKind.Utc)
        {
          // Перевод даты в UTC
          filter.FilterValue = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        if (filter.Filters != null && filter.Filters.Any())
        {
          NormalizeFilterDatesToUtc(filter.Filters);
        }
      }
    }

    public void Dispose()
    {
      _cts?.Cancel();
      _cts?.Dispose();
    }

  }
}