using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Radzen;

namespace Guard.ComponentLibrary.Gallery
{
  public partial class ViewSingleImage
  {
    [Inject]
    protected IJSRuntime JSRuntime { get; set; }
    [Inject]
    protected NavigationManager NavigationManager { get; set; }
    [Inject]
    protected NotificationService NotificationService { get; set; }
    [Parameter]
    public string ImageFolder { get; set; }
    protected async Task CancelButtonClick(MouseEventArgs args) => DialogService.Close(null);
  }
}