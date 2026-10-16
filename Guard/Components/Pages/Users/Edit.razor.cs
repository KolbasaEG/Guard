using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.Users;

public partial class Edit : IDisposable
{
  [Parameter] public string UserId { get; set; } = "";
  [Inject] protected ISecurityService Security { get; set; } = default!;
  [Inject] protected DialogService Dialogs { get; set; } = default!;
  [Inject] protected ILogger<Edit> Logger { get; set; } = default!;
  private readonly CancellationTokenSource stop = new();
  private EditUserDto model = new();
  private IReadOnlyList<UserPersonalOption> personals = [];
  private bool busy, loaded, isRoot;
  private string? error;

  protected override async Task OnInitializedAsync()
  {
    busy = true;
    try {
      var data = await Security.GetUserEditAsync(UserId, stop.Token);
      model = data.User; personals = data.Personals; isRoot = data.IsRoot; loaded = true;
    }
    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    catch (Exception ex) { HandleError(ex, "Не удалось загрузить пользователя. Проверьте доступ и повторите попытку."); }
    finally { busy = false; }
  }

  private async Task SaveAsync()
  {
    if (busy || !loaded) return;
    busy = true; error = null;
    try {
      var result = await Security.UpdateUserDetailsAsync(model, stop.Token);
      if (!result.Succeeded) { error = "Не удалось сохранить пользователя. Проверьте введённые данные."; return; }
      Dialogs.Close(new Guard.Components.Library.Dialogs.EntityDialogResult(true));
    }
    catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    catch (Exception ex) { HandleError(ex, "Не удалось сохранить пользователя. Проверьте данные и повторите попытку."); }
    finally { busy = false; }
  }

  private void HandleError(Exception ex, string fallback)
  {
    Logger.LogWarning(ex, "Редактирование пользователя {UserId}", UserId);
    error = ex switch {
      UnauthorizedAccessException => "Редактирование недоступно: проверьте права и подразделение сотрудника.",
      ArgumentException or InvalidOperationException or KeyNotFoundException or UserSecurityException => ex.Message,
      _ => fallback
    };
  }
  private void Cancel() => Dialogs.Close(null);
  public void Dispose() { stop.Cancel(); stop.Dispose(); }
}
