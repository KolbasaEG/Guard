using Guard.ComponentLibrary.Loading;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace Guard.Components.Pages.Administrator.Roles
{
  public partial class Roles
  {
    [Inject]
    protected IJSRuntime JSRuntime { get; set; }
    [Inject]
    protected NotificationService NotificationService { get; set; }
    [Inject]
    protected DialogService DialogService { get; set; }
    [Inject]
    protected IUserManagementService UserManagementService { get; set; }

    protected IEnumerable<ApplicationRole> data; 
    protected IEnumerable<ApplicationRole> filteredData; 
    protected RadzenDataGrid<ApplicationRole> grid;

    //protected RadzenDataFilter<ApplicationUser> dataFilter;

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
      await Task.Yield();
      try
      {
        var query = UserManagementService.GetAllRolesAsQueryable();
        if (!string.IsNullOrEmpty(args.OrderBy))
        {
          query = query.OrderBy(args.OrderBy);
        }
        count = query.Count();
        filteredData = query.Skip(args.Skip.Value).Take(args.Top.Value).ToList();
      }
      catch (Exception ex)
      {
        NotificationService.Notify(new NotificationMessage
        {
          Severity = NotificationSeverity.Error,
          Summary = $"Ошибка!",
          Detail = $"Произошла ошибка при загрузке данных"
        });
      }
      isLoading = false;
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
    protected async Task EditRow(ApplicationRole item)
    {
      var result = await DialogService.OpenAsync<Edit>("", new Dictionary<string, object> { { "Item", item } }, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
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
    protected async Task GridArchiveButtonClick(MouseEventArgs args, ApplicationRole item)
    {
    }
    protected async Task GridUnarchiveButtonClick(MouseEventArgs args, ApplicationRole item)
    {
    }
    protected async Task GridDeleteButtonClick(MouseEventArgs args, ApplicationRole item)
    {
      if (await DialogService.Confirm("Вы действительно хотите удалить пользователя?", "Удаление", new ConfirmOptions { OkButtonText = "Удалить", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          var result = await UserManagementService.DeleteRoleAsync(item.Name);
          if (result == IdentityResult.Success)
          {
            NotificationService.Notify(new NotificationMessage
            {
              Severity = NotificationSeverity.Success,
              Summary = $"Информационное",
              Detail = $"Запись удалена!",
              Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
            });
            await grid.Reload();
          }
          if (result != IdentityResult.Success)
          {
            NotificationService.Notify(new NotificationMessage
            {
              Severity = NotificationSeverity.Error,
              Summary = $"Внимание!",
              Detail = "" + result.Errors.FirstOrDefault().Description,
              Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
            });
          }
          ;
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
    }
    protected async Task ChahgeTenn()
    {
    }
    private async Task OnExportClick()
    {
    }
    async Task ApplyFilter()
    {

    }
  }
}