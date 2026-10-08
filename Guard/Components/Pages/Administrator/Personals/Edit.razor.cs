using Guard.Core.Services.DTOs;
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
    catch (Exception ex) { Logger.LogError(ex, "Загрузка формы сотрудника"); ShowError(ex); Dialogs.Close(null); }
    finally { isLoading = false; }
  }
  protected async Task FormSubmit()
  {
    isLoading = true;
    try { await Service.UpdateFromDtoAsync(new EditPersonalDto(item.Id, item.Version, PersonalFieldsDto.From(item)), cts.Token); Dialogs.Close(new Guard.Components.Library.Dialogs.EntityDialogResult(true)); }
    catch (OperationCanceledException) { }
    catch (Exception ex) { Logger.LogError(ex, "Сохранение сотрудника"); ShowError(ex); }
    finally { isLoading = false; }
  }
  protected void HandleCancelButtonClick() { cts.Cancel(); Dialogs.Close(null); }
  protected async Task OpenIpAssignmentsAsync()
  {
    try {
      await Permissions.RequireAsync(Guard.Core.Identity.Permissions.IpAddresses.Manage, cts.Token);
      await Dialogs.OpenAsync<IpAssignments>("", new Dictionary<string, object> { ["PersonalId"] = Id },
        new DialogOptions { Width = "650px", ShowTitle = false, ContentCssClass = "rz-p-1" });
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
    catch (Exception ex) {
      Logger.LogWarning(ex, "Открытие назначения IP сотруднику");
      ShowError(ex);
    }
  }
  public void Dispose() { cts.Cancel(); cts.Dispose(); }
  private void ShowError(Exception ex) => Notifications.Notify(new NotificationMessage {
    Severity = NotificationSeverity.Error, Summary = "Сотрудник", Detail = UserOperationErrors.Message(ex),
    Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
  });
}
