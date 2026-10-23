using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Guard.Components.Library.Templates;

internal sealed class TableLayoutInterop(IJSRuntime js) : IAsyncDisposable
{
  internal const string ModulePath = "./Components/Library/Templates/TableWrapperWithFilterAndAnalytics.razor.js";
  private const string InitializeMethod = "initialize";
  private const string DisposeMethod = "dispose";
  private IJSObjectReference? module;
  private IJSObjectReference? monitor;

  public async Task InitializeAsync(ElementReference element, DotNetObjectReference<TableWrapperWithFilterAndAnalytics>? callback = null)
  {
    module = await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
    monitor = await module.InvokeAsync<IJSObjectReference>(InitializeMethod, element, callback);
  }

  public async ValueTask DisposeAsync()
  {
    try
    {
      var currentMonitor = monitor;
      monitor = null;
      var currentModule = module;
      module = null;
      if (currentMonitor != null) {
        await currentMonitor.InvokeVoidAsync(DisposeMethod);
        await currentMonitor.DisposeAsync();
      }
      if (currentModule != null) await currentModule.DisposeAsync();
    }
    catch (JSDisconnectedException) { }
  }
}
