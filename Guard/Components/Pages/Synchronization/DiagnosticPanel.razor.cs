using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.Synchronization;

public partial class DiagnosticPanel : IDisposable
{
  [Inject] protected IApiDiagnosticService Service { get; set; } = default!;
  [Inject] protected ILogger<DiagnosticPanel> Logger { get; set; } = default!;
  [Inject] protected NotificationService Notifications { get; set; } = default!;
  [CascadingParameter] public UserAccessSnapshot? Access { get; set; }
  public sealed class DiagnosticForm { public DateTime? UpdatedAt { get; set; } }
  private readonly DiagnosticForm form = new();
  private CancellationTokenSource? operation;
  private ApiDiagnosticResult? result;
  private bool busy, disposed;
  private string phase = "";

  private async Task RunAsync()
  {
    if (busy || disposed || Access?.IsRoot != true) return;
    if (form.UpdatedAt == null) { Notify(NotificationSeverity.Warning, "Укажите дату изменений."); return; }
    busy = true; result = null; phase = "Подготовка диагностики";
    using var cancellation = new CancellationTokenSource();
    operation = cancellation;
    var progress = new Progress<string>(value =>
    {
      if (!disposed && busy) _ = InvokeAsync(() => { phase = value; StateHasChanged(); });
    });
    try
    {
      var completed = await Service.RunAsync(DateOnly.FromDateTime(form.UpdatedAt.Value), cancellation.Token, progress);
      if (!disposed)
      {
        result = completed;
        if (completed.Cancelled) Notify(NotificationSeverity.Info, "Диагностика отменена.");
        else if (!string.IsNullOrWhiteSpace(completed.Error) || !completed.FetchComplete || !completed.AnalysisComplete)
          Notify(NotificationSeverity.Error, string.IsNullOrWhiteSpace(completed.Error)
            ? "Диагностика не завершена. Подробности — в протоколе." : completed.Error);
        else if (completed.Issues > 0)
          Notify(NotificationSeverity.Warning, $"Диагностика завершена. Найдено нарушений: {completed.Issues}. Подробности — в протоколе.");
        else Notify(NotificationSeverity.Success, "Диагностика завершена. Нарушений не обнаружено.");
      }
    }
    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
    { Notify(NotificationSeverity.Info, "Диагностика отменена."); }
    catch (Exception ex)
    {
      Logger.LogWarning("Не удалось начать диагностику API, тип ошибки {DiagnosticExceptionType}", ex.GetType().Name);
      Notify(NotificationSeverity.Error, UserOperationErrors.Message(ex));
    }
    finally { operation = null; busy = false; }
  }

  private void Cancel() => operation?.Cancel();
  private void Notify(NotificationSeverity severity, string detail)
  {
    if (disposed) return;
    Notifications.Notify(new NotificationMessage { Severity = severity, Summary = "Диагностика API", Detail = detail,
      Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" });
  }
  public void Dispose() { disposed = true; operation?.Cancel(); }
}
