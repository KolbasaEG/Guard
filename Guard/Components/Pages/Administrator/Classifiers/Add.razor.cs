using Guard.ComponentLibrary.Loading;
using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;


namespace Guard.Components.Pages.Administrator.Classifiers
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
    protected IClassifierService ClassifierService { get; set; }

    protected LoadingIndicator loading;
    protected bool isLoading = false;
    protected Classifier item; 

    protected override async Task OnInitializedAsync()
    {
      item = new();
    }
    protected async Task FormSubmit()
    {
      try
      {
        isLoading = true;
        ClassifierService.Add(item);
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
      } finally 
      { 
        isLoading = false; 
      }
    }
    
    protected async Task HandleCancelButtonClick()
    {
      DialogService.Close(null);
    }   
  }
}