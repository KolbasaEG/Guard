using Guard.Core.Services.DTOs;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Shared;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.IpAddresses;

public partial class Edit : IDisposable
{
  [Inject] protected DialogService DialogService { get; set; } = default!;
  [Inject] protected NotificationService NotificationService { get; set; } = default!;
  [Inject] protected IIpAddressService IpAddressService { get; set; } = default!;
  [Inject] protected ILogger<Edit> Logger { get; set; } = default!;
  [CascadingParameter] protected UserContext? UserContext { get; set; }
  [Parameter] public Guid Id { get; set; }
  [Parameter] public bool ReadOnly { get; set; }

  private readonly CancellationTokenSource _cts = new();
  private bool disposed;
  private bool infoOpen;
  protected bool isLoading;
  protected bool isSaving;
  protected bool isLoaded;
  protected IpAddressDetailsDto item = new();
  protected IEnumerable<IpSubdivisionOptionDto> subdivisions = [];

  protected override async Task OnInitializedAsync()
  {
    var ct = _cts.Token;
    isLoading = true;
    try
    {
      var loadedItem = await IpAddressService.GetDetailsAsync(Id, ct);
      if (disposed || ct.IsCancellationRequested) return;
      if (loadedItem == null)
      {
        ShowErrorNotification("IP-адрес не найден или недоступен. Обновите список.");
        DialogService.Close(null);
        return;
      }
      if (!ReadOnly && loadedItem.Status is Guard.Core.Enums.Status.Inserted or Guard.Core.Enums.Status.Modified)
      {
        var options = await IpAddressService.GetSubdivisionOptionsAsync(ct);
        if (disposed || ct.IsCancellationRequested) return;
        subdivisions = options;
      }
      item = loadedItem;
      isLoaded = true;
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex)
    {
      if (disposed || ct.IsCancellationRequested) return;
      Logger.LogError(ex, "Загрузка IP-адреса {IpAddressId}", Id);
      ShowErrorNotification(UserOperationErrors.Message(ex));
      DialogService.Close(null);
    }
    finally { isLoading = false; }
  }

  protected async Task FormSubmit()
  {
    if (disposed || !isLoaded || ReadOnly || isSaving || _cts.IsCancellationRequested ||
        item.Status is not (Guard.Core.Enums.Status.Inserted or Guard.Core.Enums.Status.Modified)) return;
    var ct = _cts.Token;
    isSaving = true;
    try
    {
      await IpAddressService.UpdateAsync(new EditIpAddressDto(item.Id, item.Address, item.Description, item.SubdivisionId), ct);
      if (disposed || ct.IsCancellationRequested) return;
      DialogService.Close(new Guard.Components.Library.Dialogs.EntityDialogResult(true, item.Id));
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex)
    {
      if (disposed || ct.IsCancellationRequested) return;
      Logger.LogError(ex, "Обновление IP-адреса {IpAddressId}", item.Id);
      ShowErrorNotification(UserOperationErrors.Message(ex));
    }
    finally { isSaving = false; }
  }

  protected void HandleCancelButtonClick()
  {
    if (disposed || _cts.IsCancellationRequested) return;
    _cts.Cancel();
    DialogService.Close(null);
  }

  private void ShowErrorNotification(string detail)
  {
    if (disposed || _cts.IsCancellationRequested) return;
    NotificationService.Notify(new NotificationMessage {
      Severity = NotificationSeverity.Error, Summary = "IP-адрес",
      Detail = detail,
      Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
    });
  }

  protected async Task InfoButtonClick()
  {
    if (disposed || !isLoaded || isLoading || isSaving || infoOpen || _cts.IsCancellationRequested) return;
    infoOpen = true;
    try
    {
      await DialogService.OpenAsync<DialogInfo>("", new Dictionary<string, object?> { { "IpAddressDetails", item }, { "Compact", true } },
        new DialogOptions { Width = "640px", Style = "max-width:calc(100vw - 24px);", ShowTitle = false, ContentCssClass = "rz-p-1" });
    }
    finally { infoOpen = false; }
  }

  public void Dispose()
  {
    if (disposed) return;
    disposed = true;
    _cts.Cancel();
    _cts.Dispose();
  }
}
