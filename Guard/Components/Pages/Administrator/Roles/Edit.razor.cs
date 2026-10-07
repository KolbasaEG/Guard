using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;
namespace Guard.Components.Pages.Administrator.Roles;
public partial class Edit : IDisposable
{
  [Inject] protected IRoleAccessService RoleService { get; set; } = default!;
  [Inject] protected DialogService DialogService { get; set; } = default!;
  [Inject] protected NotificationService Notifications { get; set; } = default!;
  [Inject] protected ILogger<Edit> Logger { get; set; } = default!;
  [Parameter] public string Id { get; set; } = "";
  protected RoleEditDto item = new();
  protected bool isLoading;
  private readonly CancellationTokenSource stop = new();
  protected override async Task OnInitializedAsync()
  {
    isLoading = true;
    try { item = await RoleService.GetAsync(Id, stop.Token); }
    catch (Exception ex) { ShowError(ex); DialogService.Close(null); }
    finally { isLoading = false; }
  }
  protected async Task FormSubmit()
  {
    isLoading = true;
    try { await RoleService.SaveAsync(item, stop.Token); DialogService.Close(true); }
    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    catch (Exception ex) { ShowError(ex); }
    finally { isLoading = false; }
  }
  private void ShowError(Exception ex)
  {
    Logger.LogError(ex, "Ошибка сохранения роли");
    Notifications.Notify(NotificationSeverity.Error, "Роль не сохранена",
        ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException ? ex.Message : "Не удалось выполнить операцию.");
  }
  protected void HandleCancelButtonClick() { stop.Cancel(); DialogService.Close(null); }
  public void Dispose() { stop.Cancel(); stop.Dispose(); }
}
