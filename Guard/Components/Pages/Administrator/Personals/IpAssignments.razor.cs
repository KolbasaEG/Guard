using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.Administrator.Personals;

public partial class IpAssignments : IDisposable
{
  [Inject] protected IPersonalIpService Assignments { get; set; } = default!;
  [Inject] protected DialogService Dialogs { get; set; } = default!;
  [Inject] protected NotificationService Notifications { get; set; } = default!;
  [Inject] protected ILogger<IpAssignments> Logger { get; set; } = default!;
  [Parameter] public Guid PersonalId { get; set; }

  protected IReadOnlyList<IpOptionDto> available = [];
  protected IEnumerable<Guid> selected = [];
  protected bool loading, loaded;
  protected int hiddenAssignmentCount;
  protected string? error;
  private readonly CancellationTokenSource cts = new();
  private CancellationTokenSource? initializing;
  private Guid? loadedId;
  private bool disposed;

  protected override async Task OnParametersSetAsync()
  {
    if (disposed || loadedId == PersonalId) return;
    loadedId = PersonalId;
    initializing?.Cancel();
    var request = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
    initializing = request;
    loading = true;
    loaded = false;
    error = null;
    available = [];
    selected = [];
    hiddenAssignmentCount = 0;
    try
    {
      var data = await Assignments.GetAsync(PersonalId, request.Token);
      request.Token.ThrowIfCancellationRequested();
      if (disposed || !ReferenceEquals(initializing, request)) return;
      available = data.Available;
      selected = data.Selected.ToArray();
      hiddenAssignmentCount = data.HiddenAssignmentCount;
      loaded = true;
    }
    catch (OperationCanceledException) when (request.IsCancellationRequested) { }
    catch (Exception ex)
    {
      if (!disposed && ReferenceEquals(initializing, request)) ShowError(ex);
    }
    finally
    {
      if (ReferenceEquals(initializing, request)) { initializing = null; loading = false; }
      request.Dispose();
    }
  }

  protected async Task SaveAsync()
  {
    if (disposed || !loaded || loading) return;
    loading = true;
    error = null;
    try
    {
      await Assignments.UpdateAsync(PersonalId, selected?.ToArray() ?? [], cts.Token);
      if (!disposed) Dialogs.Close(new Guard.Components.Library.Dialogs.EntityDialogResult(true));
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
    catch (Exception ex) { ShowError(ex); }
    finally { if (!disposed) loading = false; }
  }

  protected void Close()
  {
    if (disposed) return;
    cts.Cancel();
    Dialogs.Close(null);
  }

  private void ShowError(Exception ex)
  {
    if (disposed) return;
    Logger.LogWarning(ex, "Назначение IP сотруднику {PersonalId}", PersonalId);
    error = UserOperationErrors.Message(ex);
    Notifications.Notify(new NotificationMessage
    {
      Severity = NotificationSeverity.Error, Summary = "Разрешённые IP", Detail = error,
      Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
    });
  }

  public void Dispose()
  {
    if (disposed) return;
    disposed = true;
    cts.Cancel();
    cts.Dispose();
  }
}
