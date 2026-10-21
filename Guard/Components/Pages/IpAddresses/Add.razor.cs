using Guard.Core.Services.DTOs;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.IpAddresses;

public partial class Add : IDisposable
{
  [Inject] protected DialogService DialogService { get; set; } = default!;
  [Inject] protected NotificationService NotificationService { get; set; } = default!;
  [Inject] protected IIpAddressManagementService IpAddressService { get; set; } = default!;
  [Inject] protected ILogger<Add> Logger { get; set; } = default!;
  [CascadingParameter] protected CurrentUserDisplayDto? UserContext { get; set; }

  private readonly CancellationTokenSource _cts = new();
  private bool disposed;
  protected bool isSaving;
  protected bool isLoading;
  protected IpAddressFieldsDto item = new();
  protected IEnumerable<IpSubdivisionOptionDto> subdivisions = [];

  protected override async Task OnInitializedAsync()
  {
    var ct = _cts.Token;
    isLoading = true;
    try
    {
      var options = await IpAddressService.GetSubdivisionOptionsAsync(ct);
      if (!disposed && !ct.IsCancellationRequested) subdivisions = options;
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex)
    {
      if (disposed || ct.IsCancellationRequested) return;
      Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Загрузка подразделений для IP-адреса");
      NotificationService.Notify(new NotificationMessage {
        Severity = NotificationSeverity.Error, Summary = "IP-адрес", Detail = UserOperationErrors.Message(ex),
        Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
      });
      DialogService.Close(null);
    }
    finally { isLoading = false; }
  }

  protected async Task FormSubmit()
  {
    if (disposed || isLoading || isSaving || _cts.IsCancellationRequested) return;
    var ct = _cts.Token;
    isSaving = true;
    try
    {
      var id = await IpAddressService.CreateAsync(new CreateIpAddressDto(item.Address, item.Description, item.SubdivisionId), ct);
      if (disposed || ct.IsCancellationRequested) return;
      DialogService.Close(new Guard.Components.Library.Dialogs.EntityDialogResult(true, id));
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex)
    {
      if (disposed || ct.IsCancellationRequested) return;
      Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Создание IP-адреса");
      NotificationService.Notify(new NotificationMessage {
        Severity = NotificationSeverity.Error, Summary = "IP-адрес",
        Detail = UserOperationErrors.Message(ex),
        Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
      });
    }
    finally { isSaving = false; }
  }

  protected void HandleCancelButtonClick()
  {
    if (disposed || _cts.IsCancellationRequested) return;
    _cts.Cancel();
    DialogService.Close(null);
  }

  public void Dispose()
  {
    if (disposed) return;
    disposed = true;
    _cts.Cancel();
    _cts.Dispose();
  }
}
