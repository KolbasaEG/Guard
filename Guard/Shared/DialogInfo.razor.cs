using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Shared
{
  public partial class DialogInfo
  {
    [Inject] protected Guard.Components.Library.BrowserTimeService Time { get; set; } = default!;
    [Inject] protected ILogger<DialogInfo> Logger { get; set; } = default!;
    protected override async Task OnAfterRenderAsync(bool firstRender) {
      if (!firstRender) return;
      try { await Time.InitializeAsync(); StateHasChanged(); }
      catch (Exception ex) {
        Logger.LogWarning(ex, "Часовой пояс информации о записи");
        NotificationService.Notify(new NotificationMessage {
          Severity = NotificationSeverity.Error, Summary = "Время записи",
          Detail = "Не удалось определить часовой пояс браузера. Обновите страницу.",
          Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
        });
      }
    }
    [Inject]
    protected DialogService DialogService { get; set; } = default!;
    [Inject]
    protected NotificationService NotificationService { get; set; } = default!;

    [Parameter] public IpAddressDetailsDto? IpAddressDetails { get; set; }
    [Parameter] public SubdivisionDetailsDto? SubdivisionDetails { get; set; }
    [Parameter] public bool Compact { get; set; }

    private EntityInfoDto Info => IpAddressDetails?.Info ?? SubdivisionDetails?.Info ?? new EntityInfoDto();
    private string ReadPermission => SubdivisionDetails != null ? Guard.Core.Identity.Permissions.Subdivisions.Read
      : IpAddressDetails != null ? Guard.Core.Identity.Permissions.IpAddresses.Read : "";

    protected override void OnParametersSet()
    {
      if (IpAddressDetails == null && SubdivisionDetails == null) DialogService.Close(null);
    }
  }
}
