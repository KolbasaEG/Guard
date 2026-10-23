using Guard.Components.Library;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.ExportArchive;

public partial class Retention : IDisposable
{
  [Inject] private IDataExportService Service { get; set; } = default!;
  [Inject] private DialogService Dialogs { get; set; } = default!;
  [Inject] private ILogger<Retention> Logger { get; set; } = default!;
  [Inject] private NotificationService Notifications { get; set; } = default!;
  public sealed class RetentionForm { public int Years { get; set; } = 3; }
  private RetentionForm form = new();
  private Guid version;
  private readonly CancellationTokenSource lifetime = new();
  private bool loading, saving, loaded, disposed;
  private string? error;
  protected override Task OnInitializedAsync() => LoadAsync();

  private async Task LoadAsync()
  {
    if (disposed || loading || saving) return;
    loading = true; error = null;
    var ct = lifetime.Token;
    try {
      var setting = await Service.GetRetentionAsync(ct);
      if (disposed) return;
      form = new() { Years = setting.Years }; version = setting.Version; loaded = true;
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex) { if (!disposed) { loaded = false; LogError(ex); error = UserOperationErrors.Message(ex); } }
    finally { loading = false; }
  }

  private async Task SaveAsync()
  {
    if (disposed || loading || saving || !loaded) return;
    saving = true; error = null;
    var ct = lifetime.Token;
    try { await Service.SaveRetentionAsync(form.Years, version, ct); if (!disposed) Dialogs.Close(true); }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex) {
      if (!disposed) {
        LogError(ex);
        Notifications.Notify(new NotificationMessage {
          Severity = NotificationSeverity.Error, Summary = "Настройка хранения", Detail = UserOperationErrors.Message(ex),
          Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
        });
      }
    }
    finally { saving = false; }
  }

  private void LogError(Exception ex) => Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Настройка хранения");
  public void Dispose() { if (disposed) return; disposed = true; lifetime.Cancel(); lifetime.Dispose(); }
}
