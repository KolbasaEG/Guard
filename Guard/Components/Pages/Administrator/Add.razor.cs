using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System.ComponentModel.DataAnnotations;


namespace Guard.Components.Pages.Administrator
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

    RadzenTemplateForm<CreateUserModel> form;

    protected CreateUserModel item;
    protected IEnumerable<ApplicationRole> roles;
    protected IEnumerable<string> userRoles = Enumerable.Empty<string>();

    protected override async Task OnInitializedAsync()
    {
      roles = await UserManagementService.GetAllRolesAsync();
      item = new CreateUserModel();
    }
    protected async Task FormSubmit()
    {
      if (form.EditContext.Validate())
      {

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

    public class CreateUserModel
    {
      [Required]
      public string Password { get; set; }
      [Required]
      public string ConfirmPassword { get; set; }
      [Required]
      public string Login { get; set; }
    }
  }
}