using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Layout;

public partial class NotificationBell
{
  private bool expanded, disposed;
  private string? actionError;
  protected override void OnInitialized() { Center.Changed += Changed; Center.Arrived += Arrived; }
  protected override Task OnAfterRenderAsync(bool firstRender) => firstRender ? Center.StartAsync() : Task.CompletedTask;
  private void Changed() => _ = InvokeAsync(() => { if (!disposed) StateHasChanged(); });
  private void Arrived(UserNotificationDto item) => _ = InvokeAsync(() => {
    if (disposed) return;
    Toasts.Notify(new NotificationMessage {
      Severity = item.Severity switch { UserNotificationSeverity.Error => NotificationSeverity.Error,
        UserNotificationSeverity.Warning => NotificationSeverity.Warning, UserNotificationSeverity.Success => NotificationSeverity.Success, _ => NotificationSeverity.Info },
      Summary = item.Title, Detail = item.Message,
      Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
    });
  });
  private static string IncidentText(UserNotificationDto item) => item.IsIncident ? item.IncidentActive ? " · Требует внимания" : " · Восстановлено" : "";
  private async Task Toggle() { expanded = !expanded; if (expanded) await Center.RefreshAsync(); }
  private async Task ReadAllAsync()
  {
    try { await Center.MarkAllReadAsync(); actionError = null; }
    catch (Exception ex) { actionError = "Не удалось отметить уведомления прочитанными."; Logger.LogWarning(ex, "Отметка уведомлений прочитанными"); }
  }
  private async Task OpenAsync(UserNotificationDto item)
  {
    expanded = false;
    await Dialogs.OpenAsync<Guard.Components.Pages.Notifications.Details>("", new Dictionary<string, object?> { ["Id"] = item.Id },
      new DialogOptions { Width = "700px", ShowTitle = false, ContentCssClass = "guard-entity-dialog-content" });
  }
  public void Dispose() { disposed = true; Center.Changed -= Changed; Center.Arrived -= Arrived; }
}
