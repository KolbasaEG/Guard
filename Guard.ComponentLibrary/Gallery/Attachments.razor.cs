using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Radzen;

namespace Guard.ComponentLibrary.Gallery
{
  public partial class Attachments
  {
    [Inject]
    protected IJSRuntime JSRuntime { get; set; }
    [Inject]
    protected DialogService DialogService { get; set; }
    [Inject]
    protected NotificationService NotificationService { get; set; }
    [Parameter]
    public string Folder { get; set; } = "\\FileStorage";
    [Parameter]
    public bool IsEditor { get; set; } = false;
    protected async Task CancelButtonClick(MouseEventArgs args) => DialogService.Close(null);
  }
}