using Guard.ComponentLibrary.Loading;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System.Linq.Dynamic.Core;

namespace Guard.Components.Pages.Administrator.IpAddresses
{
  public partial class IpAddresses
  {
    [Inject]
    protected IJSRuntime JSRuntime { get; set; }
    [Inject]
    protected NotificationService NotificationService { get; set; }
    [Inject]
    protected DialogService DialogService { get; set; }
    [Inject]
    protected IReadContextService ReadContext { get; set; }
    [Inject]
    protected IUserManagementService UserManagementService { get; set; }

    protected IEnumerable<IpAddress> data; 
    protected IEnumerable<IpAddress> filteredData; 
    protected RadzenDataGrid<IpAddress> grid;

    //protected RadzenDataFilter<IpAddress> dataFilter;

    int count;
    protected bool isEditor = true;
    protected bool isLoading = false;
    protected Status status = Status.Inserted;
    string pagingSummaryFormat = "Страница {0} из {1} (всего {2} записей)";

    protected LoadingIndicator loading;

    protected override void OnInitialized()
    { 
    
    }

    async Task LoadData(LoadDataArgs args)
    {
      isLoading = true;

      try
      {
        // Вызов без явного указания <IpAddress, (List<IpAddress>, int)>
        var (items, totalCount) = await ReadContext.GetRepository<IpAddress>()
            .QueryAsync(async query =>
            {
              // 1. Динамическая фильтрация Radzen (WHERE)
              //query = query.Where(dataFilter);

              // 2. Динамическая сортировка Radzen (ORDER BY)
              if (!string.IsNullOrEmpty(args.OrderBy))
                query = query.OrderBy(args.OrderBy);

              // 3. Подсчет отфильтрованных строк
              var total = await query.CountAsync();

              // 4. Пагинация (LIMIT / OFFSET)
              var pageData = await query
                  .Skip(args.Skip ?? 0)
                  .Take(args.Top ?? 10)
                  .ToListAsync();

              return (pageData, total);
            });

        filteredData = items;
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
    protected async Task EditRow(IpAddress item)
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
    protected async Task GridArchiveButtonClick(MouseEventArgs args, IpAddress item)
    {
      if (await DialogService.Confirm("Вы действительно хотите поместить запись в архив?", "Архивирование", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await UserManagementService.ArchiveIpAddressAsync(item.Id, ct: CancellationToken.None);
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
    protected async Task GridUnarchiveButtonClick(MouseEventArgs args, IpAddress item)
    {
      if (await DialogService.Confirm("Вы действительно хотите извлечь запись из архива?", "Извлечение из архива", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await UserManagementService.RestoreIpAddressAsync(item.Id, ct: CancellationToken.None);
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
    protected async Task GridDeleteButtonClick(MouseEventArgs args, IpAddress item)
    {
      if (await DialogService.Confirm("Вы действительно хотите удалить запись?", "Удаление", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await UserManagementService.SoftDeleteIpAddressAsync(item.Id, ct: CancellationToken.None);
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

    protected async Task ChangeSetClick()
    {
      await grid.Reload();
    }
    protected async Task ChahgeTenn()
    {
      await grid.Reload();
    }
    private async Task OnExportClick()
    {
    }
    async Task ApplyFilter()
    {

    }
  }
}