using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Identity;
using Microsoft.AspNetCore.Components;
using Radzen;
namespace Guard.Components.Pages.Administrator.Personals;
public partial class Edit : IDisposable
{
  [Inject] public IPersonalService Service { get; set; } = default!;
  [Inject] public IPermissionService Permissions { get; set; } = default!;
  [Inject] public DialogService Dialogs { get; set; } = default!;
  [Inject] public NotificationService Notifications { get; set; } = default!;
  [Inject] public ILogger<Edit> Logger { get; set; } = default!;
  [Parameter] public Guid Id { get; set; }
  [CascadingParameter] public Guard.Core.Services.DTOs.UserAccessSnapshot? Access { get; set; }
  private readonly CancellationTokenSource cts = new();
  protected Personal item = new();
  protected IEnumerable<Subdivision> subdivisions = [];
  protected bool isLoading;
  protected override async Task OnInitializedAsync()
  {
    isLoading = true;
    try {
      await Permissions.RequireAsync(Guard.Core.Identity.Permissions.Personals.Write, cts.Token);
      item = await Service.GetByIdAsync(Id, cts.Token) ?? throw new KeyNotFoundException("Сотрудник недоступен.");
      subdivisions = await Service.GetAllActiveSubdivisionsAsync(cts.Token);
    } catch (OperationCanceledException) { }
    catch (Exception ex) { Logger.LogError(ex, "Загрузка формы сотрудника"); Notifications.Notify(NotificationSeverity.Error, "Сотрудник", "Не удалось открыть запись."); Dialogs.Close(null); }
    finally { isLoading = false; }
  }
  protected async Task FormSubmit()
  {
    isLoading = true;
    try { await Service.UpdateAsync(item, cts.Token); Dialogs.Close(true); }
    catch (OperationCanceledException) { }
    catch (Exception ex) { Logger.LogError(ex, "Сохранение сотрудника"); Notifications.Notify(NotificationSeverity.Error, "Сотрудник", "Не удалось сохранить запись. Проверьте данные и права."); }
    finally { isLoading = false; }
  }
  protected void HandleCancelButtonClick() { cts.Cancel(); Dialogs.Close(null); }
  protected async Task OpenIpAssignmentsAsync()
  {
    try {
      await Permissions.RequireAsync(Guard.Core.Identity.Permissions.IpAddresses.Manage, cts.Token);
      await Dialogs.OpenAsync<IpAssignments>("", new Dictionary<string, object> { ["PersonalId"] = Id },
        new DialogOptions { Width = "650px", ShowTitle = false });
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
    catch (Exception ex) {
      Logger.LogWarning(ex, "Открытие назначения IP сотруднику");
      Notifications.Notify(NotificationSeverity.Error, "IP-адреса", "Не удалось открыть назначения. Проверьте права доступа.");
    }
  }
  public void Dispose() { cts.Cancel(); cts.Dispose(); }
}
