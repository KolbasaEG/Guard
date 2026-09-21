using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;


namespace Guard.Components.Pages.Administrator.IpAddresses
{
  public partial class Add
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

    protected IpAddress item;

    protected override async Task OnInitializedAsync()
    {
      item = new IpAddress();
    }
    protected async Task FormSubmit()
    {
      try
      {
        var createdIp = await UserManagementService.CreateIpAddressAsync(
            address: item.Address,
            name: item.Name,
            description: item.Description,
            ct: CancellationToken.None);
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
    
    protected async Task HandleCancelButtonClick()
    {
      DialogService.Close(null);
    }   
  }
}