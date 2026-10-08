using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;
namespace Guard.Components.Pages.Administrator.Roles;
public partial class Add : IDisposable
{
  [Inject] protected IRoleAccessService RoleService { get; set; } = default!;
  [Inject] protected DialogService DialogService { get; set; } = default!;
  [Inject] protected NotificationService Notifications { get; set; } = default!;
  [Inject] protected ILogger<Add> Logger { get; set; } = default!;

  protected RoleEditDto item = new();
  protected bool isLoading;
  private readonly CancellationTokenSource stop = new();
  protected override async Task OnInitializedAsync()
  {
    await Task.CompletedTask;
  }
  protected async Task FormSubmit()
  {
    isLoading = true;
    try { await RoleService.SaveAsync(item, stop.Token); DialogService.Close(new Guard.Components.Library.Dialogs.EntityDialogResult(true)); }
    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    catch (Exception ex) { ShowError(ex); }
    finally { isLoading = false; }
  }
  private void ShowError(Exception ex)
  {
    Logger.LogError(ex, "Ошибка сохранения роли");
    Notifications.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Роль не сохранена", Detail =
        ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException ? ex.Message : "Не удалось выполнить операцию.", Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" });
  }
  protected void HandleCancelButtonClick() { stop.Cancel(); DialogService.Close(null); }
  public void Dispose() { stop.Cancel(); stop.Dispose(); }
}
