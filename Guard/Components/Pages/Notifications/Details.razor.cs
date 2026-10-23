using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.Notifications;

public partial class Details
{
  [Parameter] public Guid Id { get; set; }
  private UserNotificationDto? item;
  private bool loading = true, disposed, opening;
  private string? error;
  private readonly CancellationTokenSource stopping = new();
  protected override async Task OnInitializedAsync()
  {
    try {
      var result = await Service.GetAsync(Id, stopping.Token);
      if (disposed) return;
      item = result;
      if (item == null) error = "Уведомление недоступно или удалено.";
      else await Service.MarkReadAsync(item.Id, item.Revision, stopping.Token);
    } catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
    catch (Exception ex) { if (!disposed) { item = null; error = "Не удалось открыть уведомление."; } Logger.LogWarning(ex, "Открытие уведомления"); }
    finally { if (!disposed) loading = false; }
  }
  private async Task OpenTargetAsync()
  {
    if (disposed || opening) return;
    opening = true;
    try {
      var current = await Service.GetAccessibleTargetAsync(Id, stopping.Token);
      if (current.Target == NotificationTarget.Export && current.TargetId is Guid exportId) {
        await Exports.GetAsync(exportId, stopping.Token);
        if (disposed) return;
        await Dialogs.OpenAsync<Guard.Components.Exports.ExportProgress>("", new Dictionary<string, object?> { ["JobId"] = exportId },
          new DialogOptions { Width = "700px", ShowTitle = false, ContentCssClass = "rz-p-1" });
      } else {
        if (!(await Permissions.GetCurrentAsync(stopping.Token)).IsRoot) throw new UnauthorizedAccessException();
        if (current.Target == NotificationTarget.TechnicalLogs) await Permissions.RequireAsync(Guard.Core.Identity.Permissions.Logs.Read, stopping.Token);
        if (disposed) return;
        Dialogs.Close(null);
        Navigation.NavigateTo(current.Target == NotificationTarget.TechnicalLogs ? "/Logs" : "/synchronization");
      }
    } catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
    catch (Exception ex) {
      if (!disposed) Notifications.Notify(new NotificationMessage {
        Severity = NotificationSeverity.Error, Summary = "Уведомление",
        Detail = "Связанное событие недоступно. Возможно, изменились права или истёк срок хранения.",
        Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
      });
      Logger.LogWarning(ex, "Переход из уведомления");
    }
    finally { opening = false; }
  }
  public void Dispose() { if (disposed) return; disposed = true; stopping.Cancel(); stopping.Dispose(); }
}
