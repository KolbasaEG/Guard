using Guard.Core.Services.DTOs;
using Guard.Core.Services;

using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.Classifiers;

public partial class Add : IDisposable
{
  [Inject] protected DialogService DialogService { get; set; } = default!;
  [Inject] protected NotificationService NotificationService { get; set; } = default!;
  [Inject] protected IClassifierService ClassifierService { get; set; } = default!;
  [Inject] protected ILogger<Add> Logger { get; set; } = default!;

  private readonly CancellationTokenSource _cts = new();
  private bool disposed;
  protected bool isLoading;
  protected bool isSaving;
  protected bool isLoaded = true;
  protected ClassifierDetailsDto item = new();

  protected async Task FormSubmit()
  {
    if (disposed || !isLoaded || isSaving || _cts.IsCancellationRequested) return;
    var ct = _cts.Token;
    isSaving = true;
    try {
      await ClassifierService.CreateFromDtoAsync(new CreateClassifierDto(item.ToFields()), ct);
      if (disposed || ct.IsCancellationRequested) return;
      DialogService.Close(new Guard.Components.Library.Dialogs.EntityDialogResult(true));
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex) { ShowError(ex); }
    finally { isSaving = false; }
  }

  protected void HandleCancelButtonClick()
  {
    if (disposed || _cts.IsCancellationRequested) return;
    _cts.Cancel();
    DialogService.Close(null);
  }

  private void ShowError(Exception ex)
  {
    if (disposed || _cts.IsCancellationRequested) return;
    Logger.LogError(ex, "Операция с классификатором {ClassifierId}", item.Id);
    NotificationService.Notify(new NotificationMessage {
      Severity = NotificationSeverity.Error, Summary = "Классификатор",
      Detail = UserOperationErrors.Message(ex),
      Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
    });
  }

  public void Dispose()
  {
    if (disposed) return;
    disposed = true;
    _cts.Cancel();
    _cts.Dispose();
  }
}
