using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Identity;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.Personals;

public partial class Edit : IDisposable
{
  [Inject] protected IPersonalEditorService Service { get; set; } = default!;
  [Inject] protected IPermissionService Permissions { get; set; } = default!;
  [Inject] protected DialogService Dialogs { get; set; } = default!;
  [Inject] protected NotificationService Notifications { get; set; } = default!;
  [Inject] protected ILogger<Edit> Logger { get; set; } = default!;
  [Parameter] public Guid Id { get; set; }
  [CascadingParameter] public UserAccessSnapshot? Access { get; set; }
  [CascadingParameter] public UserContext? UserContext { get; set; }

  private readonly CancellationTokenSource cts = new();
  private CancellationTokenSource? loading;
  private Guid? loadedId;
  private UserContext? loadedContext;
  private PersonalFieldsDto? originalFields;
  private bool disposed;
  protected PersonalEditorDto item = new();
  protected IEnumerable<PersonalSubdivisionOptionDto> subdivisions = [];
  protected bool isLoading, loaded;
  protected string? error;

  protected override async Task OnParametersSetAsync()
  {
    if (disposed || Access == null || loadedId == Id && ReferenceEquals(loadedContext, UserContext)) return;
    var reloadItem = item.Id != Id;
    loadedId = Id;
    loadedContext = UserContext;
    loading?.Cancel();
    var request = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
    loading = request;
    loaded = false;
    isLoading = true;
    error = null;
    try
    {
      await Permissions.RequireAsync(Guard.Core.Identity.Permissions.Personals.Write, request.Token);
      var result = reloadItem ? await Service.GetEditorAsync(Id, request.Token) : item;
      if (result == null) throw new KeyNotFoundException("Сотрудник недоступен.");
      if (result.Status is not (Status.Inserted or Status.Modified))
        throw new EntityRuleException("Редактирование недоступно: сотрудник заблокирован, архивирован или удалён.");
      var options = await Service.GetSubdivisionOptionsAsync(request.Token);
      request.Token.ThrowIfCancellationRequested();
      if (disposed || !ReferenceEquals(loading, request)) return;
      item = result;
      if (reloadItem) originalFields = result.ToFields();
      subdivisions = options;
      loaded = true;
    }
    catch (OperationCanceledException) when (request.IsCancellationRequested) { }
    catch (Exception ex)
    {
      if (!disposed && ReferenceEquals(loading, request)) ShowError(ex, "Загрузка формы сотрудника");
    }
    finally
    {
      if (ReferenceEquals(loading, request)) { loading = null; isLoading = false; }
      request.Dispose();
    }
  }

  protected async Task FormSubmit()
  {
    if (disposed || !loaded || isLoading) return;
    isLoading = true;
    error = null;
    try
    {
      await Service.UpdateFromDtoAsync(new EditPersonalDto(item.Id, item.Version, item.ToFields()), cts.Token);
      if (!disposed) Dialogs.Close(new Guard.Components.Library.Dialogs.EntityDialogResult(true));
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
    catch (Exception ex) { ShowError(ex, "Сохранение сотрудника"); }
    finally { if (!disposed) isLoading = false; }
  }

  protected void HandleCancelButtonClick()
  {
    if (disposed) return;
    cts.Cancel();
    Dialogs.Close(null);
  }

  protected async Task OpenIpAssignmentsAsync()
  {
    if (disposed || !loaded || isLoading) return;
    try
    {
      await Permissions.RequireAsync(Guard.Core.Identity.Permissions.IpAddresses.Manage, cts.Token);
      var result = await Dialogs.OpenAsync<IpAssignments>("", new Dictionary<string, object?> { ["PersonalId"] = item.Id },
        new DialogOptions { Width = "650px", ShowTitle = false, ContentCssClass = "rz-p-1" });
      if (!disposed && Guard.Components.Library.Dialogs.EntityDialogResult.IsSuccess(result))
      {
        Notifications.Notify(new NotificationMessage
        {
          Severity = NotificationSeverity.Success, Summary = "Сотрудник", Detail = "Назначения IP сохранены.",
          Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
        });
        loaded = false;
        var current = await Service.GetEditorAsync(item.Id, cts.Token)
          ?? throw new KeyNotFoundException("Сотрудник недоступен.");
        if (!disposed)
        {
          RefreshAfterIpSave(current);
          loaded = true;
        }
      }
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
    catch (Exception ex) { ShowError(ex, "Открытие назначения IP сотруднику"); }
  }

  protected void RefreshAfterIpSave(PersonalEditorDto current)
  {
    if (current.Id != item.Id || originalFields != current.ToFields())
      throw new EntityConflictException();
    if (current.Status is not (Status.Inserted or Status.Modified))
    {
      loaded = false;
      throw new EntityRuleException("Состояние сотрудника изменилось. Откройте форму заново.");
    }
    // Назначение IP меняет версию: сохраняем введённые поля, проверив отсутствие чужих правок.
    item.Version = current.Version;
    item.Status = current.Status;
  }

  private void ShowError(Exception ex, string operation)
  {
    if (disposed) return;
    Logger.LogWarning(ex, "{Operation}", operation);
    error = UserOperationErrors.Message(ex);
    Notifications.Notify(new NotificationMessage
    {
      Severity = NotificationSeverity.Error, Summary = "Сотрудник", Detail = error,
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
