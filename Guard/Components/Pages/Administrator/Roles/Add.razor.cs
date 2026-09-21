using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;


namespace Guard.Components.Pages.Administrator.Roles
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

    RadzenTemplateForm<ApplicationRole> form;

    protected ApplicationRole item;

    protected override async Task OnInitializedAsync()
    {
      item = new ApplicationRole();
    }
    protected async Task FormSubmit()
    {
      if (form.EditContext.Validate())
      {
        try
        {
          var result = await UserManagementService.CreateRoleAsync(item.Name);
          if (result == IdentityResult.Success) DialogService.Close(result);
          if (result != IdentityResult.Success) 
          {
            var errorMessage = result.Errors.FirstOrDefault()?.Description ?? "Неизвестная ошибка";

            NotificationService.Notify(new NotificationMessage
            {
              Severity = NotificationSeverity.Error,
              Summary = $"Внимание!",
              Detail = errorMessage,
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
      else
      {
        NotificationService.Notify(new NotificationMessage
        {
          Severity = NotificationSeverity.Error,
          Summary = $"Внимание!",
          Detail = $"Проверьте введенные данные!",
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