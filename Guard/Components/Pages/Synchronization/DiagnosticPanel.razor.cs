using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;

namespace Guard.Components.Pages.Synchronization;

public partial class DiagnosticPanel : IDisposable
{
  [Inject] protected IApiDiagnosticService Service { get; set; } = default!;
  [Inject] protected ILogger<DiagnosticPanel> Logger { get; set; } = default!;
  [CascadingParameter] public UserAccessSnapshot? Access { get; set; }
  public sealed class DiagnosticForm { public DateTime? UpdatedAt { get; set; } }
  private readonly DiagnosticForm form = new();
  private CancellationTokenSource? operation;
  private ApiDiagnosticResult? result;
  private bool busy, disposed;
  private string phase = "", error = "";

  private async Task RunAsync()
  {
    if (busy || disposed || Access?.IsRoot != true) return;
    if (form.UpdatedAt == null) { error = "Укажите дату UpdatedAt."; return; }
    busy = true; error = ""; result = null; phase = "Подготовка диагностики";
    using var cancellation = new CancellationTokenSource();
    operation = cancellation;
    var progress = new Progress<string>(value =>
    {
      if (!disposed && busy) _ = InvokeAsync(() => { phase = value; StateHasChanged(); });
    });
    try
    {
      var completed = await Service.RunAsync(DateOnly.FromDateTime(form.UpdatedAt.Value), cancellation.Token, progress);
      if (!disposed) { result = completed; error = completed.Error ?? ""; }
    }
    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
    { if (!disposed) error = "Диагностика отменена."; }
    catch (Exception ex)
    {
      Logger.LogWarning("Не удалось начать диагностику API, тип ошибки {DiagnosticExceptionType}", ex.GetType().Name);
      if (!disposed) error = UserOperationErrors.Message(ex);
    }
    finally { operation = null; busy = false; }
  }

  private void Cancel() => operation?.Cancel();
  public void Dispose() { disposed = true; operation?.Cancel(); }
}
