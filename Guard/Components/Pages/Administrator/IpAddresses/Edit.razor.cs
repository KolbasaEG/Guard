using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;


namespace Guard.Components.Pages.Administrator.IpAddresses
{
  public partial class Edit
  {
    [Inject]
    protected IJSRuntime JSRuntime { get; set; }
    [Inject]
    protected NavigationManager NavigationManager { get; set; }
    [Inject]
    protected DialogService DialogService { get; set; }
    [Inject]
    protected TooltipService TooltipService { get; set; }
    [Inject]
    protected ContextMenuService ContextMenuService { get; set; }
    [Inject]
    protected NotificationService NotificationService { get; set; }
    [Inject]
    protected IUserManagementService UserManagementService { get; set; }

    [Parameter] public Guid Id { get; set; }
    public IpAddress item { get; set; }

    protected override async Task OnInitializedAsync()
    {
      item = await UserManagementService.GetIpAddressByIdAsync(Id, CancellationToken.None);
      if(item == null)
      {
        NotificationService.Notify(new NotificationMessage
        {
          Severity = NotificationSeverity.Error,
          Summary = $"Внимание!",
          Detail = "Запись не найдена!",
          Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
        });
        DialogService.Close(null);
      }
    }
    protected async Task FormSubmit()
    {
      try
      {
        await UserManagementService.UpdateIpAddressAsync(item, ct: CancellationToken.None);
        DialogService.Close(true);
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
    protected async Task InfoButtonClick()
    {
      var baseEntity = ((object)item) as BaseEntity;
      await DialogService.OpenAsync<DialogInfo>("", new Dictionary<string, object> { { "BaseEntity", baseEntity } }, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
    }

    protected async Task HandleCancelButtonClick()
    {
      DialogService.Close(null);
    }   
  }
}