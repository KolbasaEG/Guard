using Guard.Core.Services.DTOs;
using Guard.Core.Services;
using Guard.Core.Enums;
using Guard.Shared;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.Subdivisions;

public partial class Add : IDisposable
{
  [Inject] protected DialogService DialogService { get; set; } = default!;
  [Inject] protected NotificationService NotificationService { get; set; } = default!;
  [Inject] protected ISubdivisionManagementService SubdivisionService { get; set; } = default!;
  [Inject] protected ILogger<Add> Logger { get; set; } = default!;

  private readonly CancellationTokenSource _cts = new();
  private bool disposed;
  protected bool isLoading;
  protected bool isSaving;
  protected bool isLoaded;
  protected SubdivisionDetailsDto item = new();
  protected IEnumerable<SubdivisionClassifierOptionDto> organTypes = [];
  protected IEnumerable<SubdivisionClassifierOptionDto> statusClassifiers = [];
  protected IEnumerable<SubdivisionParentOptionDto> subdivisions = [];

  protected override async Task OnInitializedAsync()
  {
    var ct = _cts.Token;
    isLoading = true;
    try {
      var options = await SubdivisionService.GetEditorOptionsAsync(ct);
      if (disposed || ct.IsCancellationRequested) return;
      subdivisions = options.Parents;
      organTypes = options.OrganTypes;
      statusClassifiers = options.StatusClassifiers;
      isLoaded = true;
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex) {
      if (disposed || ct.IsCancellationRequested) return;
      ShowError(ex);
      DialogService.Close(null);
    }
    finally { isLoading = false; }
  }

  protected async Task FormSubmit()
  {
    if (disposed || !isLoaded || isSaving || _cts.IsCancellationRequested) return;
    var ct = _cts.Token;
    isSaving = true;
    try {
      await SubdivisionService.CreateFromDtoAsync(new CreateSubdivisionDto(item.ToFields(), item.ParentId), ct);
      if (disposed || ct.IsCancellationRequested) return;
      DialogService.Close(new Guard.Components.Library.Dialogs.EntityDialogResult(true, item.Id));
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
    Logger.LogError(ex, "Операция с подразделением {SubdivisionId}", item.Id);
    NotificationService.Notify(new NotificationMessage {
      Severity = NotificationSeverity.Error, Summary = "Подразделение",
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
