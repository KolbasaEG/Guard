using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;


namespace Guard.Components.Pages.Administrator.Roles
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

    [Parameter] public ApplicationRole Item { get; set; }

    RadzenTemplateForm<ApplicationRole> form;
    protected override async Task OnInitializedAsync()
    {
    }
    protected async Task FormSubmit()
    {
      try
      {
        var result = await UserManagementService.UpdateRoleAsync(Item);
        if (result == IdentityResult.Success)
        {
          DialogService.Close(result);
        }
        else
        {
          var errorMessage = result.Errors.FirstOrDefault()?.Description ?? "Неизвестная ошибка";
          NotificationService.Notify(new NotificationMessage
          {
            Severity = NotificationSeverity.Error,
            Summary = $"Внимание!",
            Detail = "" + errorMessage,
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

    protected async Task HandleCancelButtonClick()
    {
      DialogService.Close(null);
    }   
  }
}