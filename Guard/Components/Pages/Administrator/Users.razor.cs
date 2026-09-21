using Guard.ComponentLibrary.Loading;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace Guard.Components.Pages.Administrator
{
  public partial class Users
  {
    [Inject]
    protected IJSRuntime JSRuntime { get; set; }
    [Inject]
    protected NotificationService NotificationService { get; set; }
    [Inject]
    protected DialogService DialogService { get; set; }
    [Inject]
    protected IUserManagementService UserManagementService { get; set; }

    protected IEnumerable<ApplicationUser> data; 
    protected IEnumerable<ApplicationUser> filteredData; 
    protected RadzenDataGrid<ApplicationUser> grid;

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
        var query = UserManagementService.GetAllUsersAsQueryable();
        //query = query.Where(dataFilter);
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
    protected async Task EditRow(ApplicationUser item)
    {
    }
    protected async Task GridArchiveButtonClick(MouseEventArgs args, ApplicationUser item)
    {
    }
    protected async Task GridUnarchiveButtonClick(MouseEventArgs args, ApplicationUser item)
    {
    }
    protected async Task GridDeleteButtonClick(MouseEventArgs args, ApplicationUser item)
    {
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