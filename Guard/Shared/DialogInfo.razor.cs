using Guard.ComponentLibrary.Loading;
using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.JSInterop;
using Radzen;

namespace Guard.Shared
{
  public partial class DialogInfo
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
    protected IClassifierService ClassifierService { get; set; }
    [Inject]
    protected ISecurityService Security { get; set; }


    [Parameter] public BaseEntity BaseEntity { get; set; }

    protected bool isLoading = false;
    protected string creator = string.Empty;
    protected string editor = string.Empty;
    

    protected override async Task OnInitializedAsync() 
    {
      if (BaseEntity != null)
      {
        var crtor = await Security.GetUserByIdAsync(BaseEntity.CreatedBy);
        if (crtor != null) creator = crtor.Email ?? "";
        if (!String.IsNullOrEmpty(BaseEntity.ModifiedBy))
        {
          var edtor = await Security.GetUserByIdAsync(BaseEntity.ModifiedBy);
          if (edtor != null) editor = edtor.Email ?? "";
        }
      }
      else await HandleCancelButtonClick();
    }
    protected async Task HandleCancelButtonClick()
    {
      DialogService.Close(null);
    }   
  }
}