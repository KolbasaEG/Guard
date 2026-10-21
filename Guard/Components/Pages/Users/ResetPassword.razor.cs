using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.Users;

public partial class ResetPassword : IDisposable
{
  [Inject] protected IUserAdministrationService Security { get; set; } = default!;
  [Inject] protected DialogService DialogService { get; set; } = default!;
  [Inject] protected ILogger<ResetPassword> Logger { get; set; } = default!;
  [Parameter] public string UserId { get; set; } = "";
  [CascadingParameter] public UserAccessSnapshot? Access { get; set; }
  private readonly CreateUserDto model = new() { Password = "" };
  private readonly CancellationTokenSource stop = new();
  private string confirmation = "";
  private string? error;
  private bool busy;
  private bool disposed;
  private void Cancel() { stop.Cancel(); model.Password = ""; confirmation = ""; DialogService.Close(null); }
  private async Task SaveAsync()
  {
    if (disposed || busy || stop.IsCancellationRequested) return;
    var ct = stop.Token;
    error = null;
    if (model.Password != confirmation) { error = "Пароли не совпадают."; return; }
    busy = true;
    try {
      await Security.ResetPasswordFromDtoAsync(new(UserId, model.Password), ct);
      if (disposed || ct.IsCancellationRequested) return;
      DialogService.Close(new Guard.Components.Library.Dialogs.EntityDialogResult(true));
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception) when (disposed || ct.IsCancellationRequested) { }
    catch (ArgumentException ex) { error = ex.Message; }
    catch (UnauthorizedAccessException) { error = "Недостаточно прав для сброса пароля этого пользователя."; }
    catch (UserSecurityException ex) { error = ex.Message; }
    catch (Exception ex) { Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Сброс пароля"); error = "Не удалось сбросить пароль. Обновите данные и повторите попытку."; }
    finally { busy = false; model.Password = ""; confirmation = ""; }
  }
  public void Dispose() { if (disposed) return; disposed = true; model.Password = ""; confirmation = ""; stop.Cancel(); stop.Dispose(); }
}
