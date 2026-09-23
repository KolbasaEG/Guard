//using Guard.ComponentLibrary.Loading;
//using Guard.Core.Entities;
//using Guard.Core.Enums;
//using Guard.Core.Extensions;
//using Guard.Core.Services;
//using Microsoft.AspNetCore.Components;
//using Microsoft.AspNetCore.Components.Web;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.JSInterop;
//using Radzen;
//using Radzen.Blazor;
//using System.Linq.Dynamic.Core;

//namespace Guard.Components.Pages.Administrator.Classifiers
//{
//  public partial class Index
//  {
//    [Inject]
//    protected IJSRuntime JSRuntime { get; set; }
//    [Inject]
//    protected NotificationService NotificationService { get; set; }
//    [Inject]
//    protected DialogService DialogService { get; set; }
//    [Inject]
//    protected IReadContextService ReadContext { get; set; }
//    [Inject]
//    protected IClassifierService ClassifierService { get; set; }


//    protected IEnumerable<Classifier> data; 
//    protected IEnumerable<Classifier> filteredData;
//    protected RadzenDataGrid<Classifier> grid;

//    protected RadzenDataFilter<Classifier> dataFilter;

//    int count;
//    protected bool isEditor = true;
//    protected bool isLoading = false;
//    protected bool status = true;
//    string pagingSummaryFormat = "Страница {0} из {1} (всего {2} записей)";

//    protected LoadingIndicator loading;
//    async Task LoadData(LoadDataArgs args)
//    {
//      isLoading = true;

//      try
//      {
//        NormalizeFilterDatesToUtc(dataFilter.Filters);
//        var (items, totalCount) = await ReadContext.GetRepository<Classifier>()
//        .QueryAsync(async query =>
//        {
//          query = query.Where(p => p.IsActive == status);
//          query = query.Where(dataFilter);

//          // 2. Динамическая сортировка Radzen (ORDER BY)
//          if (!string.IsNullOrEmpty(args.OrderBy))
//            query = query.OrderBy(args.OrderBy);

//          // 3. Подсчет отфильтрованных строк
//          var total = await query.CountAsync();

//          // 4. Пагинация (LIMIT / OFFSET)
//          var pageData = await query
//              .Skip(args.Skip ?? 0)
//              .Take(args.Top ?? 10)
//              .ToListAsync();

//          return (pageData, total);
//        });

//        filteredData = items.ConvertDateTimesToLocal().ToList();
//        count = totalCount;
//      }
//      finally
//      {
//        isLoading = false;
//      }
//    }

//    private void NormalizeFilterDatesToUtc(IEnumerable<CompositeFilterDescriptor> filters)
//    {
//      if (filters == null) return;

//      foreach (var filter in filters)
//      {
//        if (filter.FilterValue is DateTime dt && dt.Kind != DateTimeKind.Utc)
//        {
//          // Перевод даты в UTC
//          filter.FilterValue = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
//        }

//        if (filter.Filters != null && filter.Filters.Any())
//        {
//          NormalizeFilterDatesToUtc(filter.Filters);
//        }
//      }
//    }
//    protected async Task AddClick(MouseEventArgs args)
//    {
//      var result = await DialogService.OpenAsync<Add>("", null, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
//      if (result != null)
//      {
//        NotificationService.Notify(new NotificationMessage
//        {
//          Severity = NotificationSeverity.Success,
//          Summary = $"Информационное",
//          Detail = $"Добавлена новая запись!",
//          Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
//        });
//        await grid.Reload();
//      }
//    }
//    protected async Task EditRow(Classifier item)
//    {
//      var result = await DialogService.OpenAsync<Edit>("", new Dictionary<string, object> { { "Id", item.Id } }, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
//      if (result != null)
//      {
//        NotificationService.Notify(new NotificationMessage
//        {
//          Severity = NotificationSeverity.Success,
//          Summary = $"Информационное",
//          Detail = $"Информация обновлена!",
//          Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
//        });
//        await grid.Reload();
//      }
//    }
//    protected async Task GridDeleteButtonClick(MouseEventArgs args, Classifier item)
//    {
//      if (await DialogService.Confirm("Вы действительно хотите удалить запись?", "Удаление", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
//      {
//        try
//        {
//          ClassifierService.SoftDelete(item);
//          NotificationService.Notify(new NotificationMessage
//          {
//            Severity = NotificationSeverity.Success,
//            Summary = $"Информационное",
//            Detail = $"Запись удалена!",
//            Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
//          });
//          await grid.Reload();
//        }
//        catch (Exception ex)
//        {
//          NotificationService.Notify(new NotificationMessage
//          {
//            Severity = NotificationSeverity.Error,
//            Summary = $"Внимание!",
//            Detail = ex.Message,
//            Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
//          });
//        }
//      }
//    }
//    protected async Task GridRestoreButtonClick(MouseEventArgs args, Classifier item)
//    {
//      if (await DialogService.Confirm("Вы действительно хотите удалить запись?", "Удаление", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
//      {
//        try
//        {
//          ClassifierService.Restore(item);
//          NotificationService.Notify(new NotificationMessage
//          {
//            Severity = NotificationSeverity.Success,
//            Summary = $"Информационное",
//            Detail = $"Запись удалена!",
//            Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
//          });
//          await grid.Reload();
//        }
//        catch (Exception ex)
//        {
//          NotificationService.Notify(new NotificationMessage
//          {
//            Severity = NotificationSeverity.Error,
//            Summary = $"Внимание!",
//            Detail = ex.Message,
//            Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
//          });
//        }
//      }
//    }

//    protected async Task ChangeSetClick()
//    {
//      await grid.Reload();
//    }
//    async Task ApplyFilter() => await dataFilter.Filter();
//  }
//}