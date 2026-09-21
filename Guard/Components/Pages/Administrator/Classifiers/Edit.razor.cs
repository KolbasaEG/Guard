using Guard.ComponentLibrary.Loading;
using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Guard.Components.Pages.Administrator.Classifiers
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
    protected IClassifierService ClassifierService { get; set; }
    [Parameter] public long Id { get; set; }

    protected bool isLoading = false;
    protected Classifier item;

    protected LoadingIndicator loading;

    protected override async Task OnInitializedAsync() => item = ClassifierService.GetById(Id);
    protected async Task FormSubmit()
    {
      try
      {
        isLoading = true;
        ClassifierService.Update(item);
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
      finally 
      { 
        isLoading = false; 
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