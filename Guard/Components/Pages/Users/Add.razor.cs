using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.Users;

public partial class Add : IDisposable
{
  [Inject] protected IUserAdministrationService Security { get; set; } = default!;
  [Inject] protected DialogService Dialogs { get; set; } = default!;
  [Inject] protected ILogger<Add> Logger { get; set; } = default!;
  private readonly CreateUserDto model = new() { UserName = "", Email = "", Password = "" };
  private readonly CancellationTokenSource stop = new();
  private IReadOnlyList<UserPersonalOption> personals = [];
  private string confirmation = "";
  private string? error;
  private bool busy, loaded;
  private bool disposed;
  protected override async Task OnInitializedAsync()
  {
    busy = true;
    try {
      var options = await Security.GetUserPersonalOptionsAsync(stop.Token);
      if (disposed || stop.IsCancellationRequested) return;
      personals = options; loaded = true;
    }
    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    catch (Exception) when (disposed || stop.IsCancellationRequested) { }
    catch (Exception ex) { Logger.LogWarning(ex, "Загрузка сотрудников для создания пользователя"); error = "Не удалось загрузить сотрудников. Проверьте доступ."; }
    finally { busy = false; }
  }
  private async Task SaveAsync()
  {
    if (disposed || busy || !loaded || stop.IsCancellationRequested) return;
    var ct = stop.Token;
    error = null;
    if (model.Password != confirmation) { error = "Пароли не совпадают."; return; }
    if (model.PersonalId == null || string.IsNullOrWhiteSpace(model.UserName) ||
        !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(model.Email)) {
      error = "Заполните имя пользователя, корректный email и выберите сотрудника."; return;
    }
    busy = true;
    try {
      await Security.CreateFromDtoAsync(new(model.UserName, model.Email, model.Password, model.PersonalId.Value), ct);
      if (disposed || ct.IsCancellationRequested) return;
      model.Password = ""; confirmation = "";
      Dialogs.Close(new Guard.Components.Library.Dialogs.EntityDialogResult(true));
    }
    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    catch (Exception) when (disposed || ct.IsCancellationRequested) { }
    catch (ArgumentException ex) { error = ex.Message; }
    catch (InvalidOperationException ex) { error = ex.Message; }
    catch (UnauthorizedAccessException) { error = "Создание пользователя недоступно: проверьте права и подразделение сотрудника."; }
    catch (Exception ex) { Logger.LogError(ex, "Ошибка создания пользователя"); error = "Не удалось создать пользователя."; }
    finally { busy = false; }
  }
  private void Cancel() { stop.Cancel(); model.Password = ""; confirmation = ""; Dialogs.Close(null); }
  public void Dispose() { if (disposed) return; disposed = true; model.Password = ""; confirmation = ""; stop.Cancel(); stop.Dispose(); }
}
