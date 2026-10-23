using Guard.Components.Library;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Microsoft.AspNetCore.Components;

namespace Guard.Components.Pages.ExportArchive;

public partial class ArchiveInfo : IDisposable
{
  [Inject] private IDataExportService Service { get; set; } = default!;
  [Inject] private ILogger<ArchiveInfo> Logger { get; set; } = default!;
  [Parameter] public Guid Id { get; set; }
  private ExportArchiveDetails? details;
  private string? error;
  private bool loading, disposed;
  private readonly CancellationTokenSource lifetime = new();

  protected override async Task OnInitializedAsync()
  {
    loading = true;
    var ct = lifetime.Token;
    try {
      var result = await Service.GetArchiveDetailsAsync(Id, ct);
      if (!disposed) details = result;
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex) {
      if (!disposed) {
        Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Сведения архива {ExportId}", Id);
        error = UserOperationErrors.Message(ex);
      }
    }
    finally { loading = false; }
  }

  private static string FormatTime(DateTimeOffset? value) => value?.UtcDateTime.ToString("dd.MM.yyyy HH:mm:ss") ?? "не задано";
  public void Dispose() { if (disposed) return; disposed = true; lifetime.Cancel(); lifetime.Dispose(); }
}
