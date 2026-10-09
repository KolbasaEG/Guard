using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.Administrator.Personals;

public partial class Add : IDisposable
{
  [Inject] protected DialogService DialogService { get; set; } = default!;
  [Inject] protected NotificationService NotificationService { get; set; } = default!;
  [Inject] protected IPersonalService PersonalService { get; set; } = default!;
  [Inject] protected ILogger<Add> Logger { get; set; } = default!;
  [CascadingParameter] public UserAccessSnapshot? Access { get; set; }
  [CascadingParameter] public UserContext? UserContext { get; set; }

  private readonly CancellationTokenSource cts = new();
  private CancellationTokenSource? loading;
  private UserAccessSnapshot? loadedAccess;
  private UserContext? loadedContext;
  private bool disposed;
  protected bool isLoading, loaded;
  protected string? error;
  protected Personal item = new();
  protected IEnumerable<Subdivision> subdivisions = [];

  protected override async Task OnParametersSetAsync()
  {
    if (disposed || Access == null ||
        ReferenceEquals(loadedAccess, Access) && ReferenceEquals(loadedContext, UserContext)) return;
    loadedAccess = Access;
    loadedContext = UserContext;
    loading?.Cancel();
    var request = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
    loading = request;
    isLoading = true;
    loaded = false;
    error = null;
    try
    {
      var result = await PersonalService.GetAllActiveSubdivisionsAsync(request.Token);
      request.Token.ThrowIfCancellationRequested();
      if (disposed || !ReferenceEquals(loading, request)) return;
      subdivisions = result;
      loaded = true;
    }
    catch (OperationCanceledException) when (request.IsCancellationRequested) { }
    catch (Exception ex)
    {
      if (!disposed && ReferenceEquals(loading, request)) ShowError(ex, "Загрузка подразделений для нового сотрудника");
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
      await PersonalService.CreateFromDtoAsync(new CreatePersonalDto(PersonalFieldsDto.From(item)), cts.Token);
      if (!disposed) DialogService.Close(new Guard.Components.Library.Dialogs.EntityDialogResult(true));
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
    catch (Exception ex) { ShowError(ex, "Создание сотрудника"); }
    finally { if (!disposed) isLoading = false; }
  }

  protected void HandleCancelButtonClick()
  {
    if (disposed) return;
    cts.Cancel();
    DialogService.Close(null);
  }

  private void ShowError(Exception ex, string operation)
  {
    if (disposed) return;
    Logger.LogWarning(ex, "{Operation}", operation);
    error = UserOperationErrors.Message(ex);
    NotificationService.Notify(new NotificationMessage
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
