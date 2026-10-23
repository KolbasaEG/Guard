using Guard.Components.Library;
using Guard.Components.Library.Templates;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace Guard.Components.Pages.ExportArchive;

public partial class Preview : IAsyncDisposable
{
  [Inject] private IDataExportService Service { get; set; } = default!;
  [Inject] private ILogger<Preview> Logger { get; set; } = default!;
  [Inject] private IJSRuntime JS { get; set; } = default!;
  [Inject] public DialogService Dialogs { get; set; } = default!;
  [Parameter] public Guid Id { get; set; }
  private ExportPreview? preview;
  private int part = 1, skip;
  private bool loading, disposed;
  private string? error;
  private readonly CancellationTokenSource cts = new();
  private ElementReference element;
  private TableLayoutInterop? interop;

  protected override Task OnInitializedAsync() => LoadAsync(0);

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (!firstRender || disposed) return;
    interop = new(JS);
    await interop.InitializeAsync(element);
    if (disposed) await interop.DisposeAsync();
  }

  private async Task LoadAsync(int offset)
  {
    if (loading || disposed) return;
    loading = true; error = null;
    var ct = cts.Token;
    try {
      var result = await Service.PreviewAsync(Id, part, offset, ct);
      if (!disposed) { preview = result; skip = offset; }
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex) {
      if (!disposed) {
        preview = null;
        Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Просмотр архивной копии {ExportId}", Id);
        error = UserOperationErrors.Message(ex);
      }
    }
    finally { loading = false; }
  }

  private Task InfoAsync() => Dialogs.OpenAsync<ArchiveInfo>("", new Dictionary<string, object?> { ["Id"] = Id },
    new DialogOptions { Width = "800px", ShowTitle = false, ContentCssClass = "guard-entity-dialog-content" });

  public async ValueTask DisposeAsync()
  {
    if (disposed) return;
    disposed = true;
    cts.Cancel(); cts.Dispose();
    if (interop != null) await interop.DisposeAsync();
  }
}
