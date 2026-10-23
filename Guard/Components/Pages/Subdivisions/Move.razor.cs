using Guard.Core.Services.DTOs;
using Guard.Core.Services;
using Guard.Core.Enums;
using Guard.Shared;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.Subdivisions;

public partial class Move : IDisposable
{
  [Inject] protected DialogService DialogService { get; set; } = default!;
  [Inject] protected NotificationService NotificationService { get; set; } = default!;
  [Inject] protected ISubdivisionManagementService SubdivisionService { get; set; } = default!;
  [Inject] protected ILogger<Move> Logger { get; set; } = default!;
  [Parameter] public Guid Id { get; set; }

  private readonly CancellationTokenSource _cts = new();
  private bool disposed;
  protected bool isLoading;
  protected bool isSaving;
  protected bool isLoaded;
  protected SubdivisionDetailsDto item = new();
  protected Guid? selectedParentId;
  protected IEnumerable<SubdivisionParentOptionDto> availableParents = [];

  protected override async Task OnInitializedAsync()
  {
    var ct = _cts.Token;
    isLoading = true;
    try {
      var loaded = await SubdivisionService.GetDetailsAsync(Id, ct);
      if (disposed || ct.IsCancellationRequested) return;
      if (loaded == null) throw new KeyNotFoundException("Подразделение не найдено или недоступно. Обновите список.");
      var parents = await SubdivisionService.GetMoveParentOptionsAsync(Id, ct);
      if (disposed || ct.IsCancellationRequested) return;
      item = loaded;
      selectedParentId = loaded.ParentId;
      availableParents = parents;
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
    if (disposed || !isLoaded || isSaving || _cts.IsCancellationRequested || item.Status is not (Status.Inserted or Status.Modified)) return;
    var ct = _cts.Token;
    isSaving = true;
    try {
      await SubdivisionService.MoveAsync(item.Id, selectedParentId, ct, item.Version);
      if (disposed || ct.IsCancellationRequested) return;
      DialogService.Close(new Guard.Components.Library.Dialogs.EntityDialogResult(true, item.Id));
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex) { ShowError(ex); }
    finally { isSaving = false; }
  }

  private bool infoOpen;
  protected async Task InfoButtonClick()
  {
    if (disposed || !isLoaded || isLoading || isSaving || infoOpen || _cts.IsCancellationRequested) return;
    infoOpen = true;
    try {
      await DialogService.OpenAsync<DialogInfo>("", new Dictionary<string, object?> { { "SubdivisionDetails", item }, { "Compact", true } },
        new DialogOptions { Width = "800px", ShowTitle = false, ContentCssClass = "guard-entity-dialog-content" });
    }
    finally { infoOpen = false; }
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
    Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Операция с подразделением {SubdivisionId}", item.Id);
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
