using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Identity;

namespace Guard.Core.Identity;

// Request-scoped Identity adapter: changing a password also replaces the HTTP cookie.
public sealed class OwnPasswordService(IHttpContextAccessor contexts,
  UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
  ILogger<OwnPasswordService> logger) : IOwnPasswordService
{
  private async Task<ApplicationUser?> GetUserAsync(CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();
    var context = contexts.HttpContext;
    if (context?.User.Identity?.IsAuthenticated != true) return null;
    return await users.GetUserAsync(context.User);
  }

  public async Task<OwnPasswordState> GetStateAsync(CancellationToken ct = default)
  {
    var user = await GetUserAsync(ct);
    if (user is null) return OwnPasswordState.MissingUser;
    ct.ThrowIfCancellationRequested();
    return await users.HasPasswordAsync(user) ? OwnPasswordState.Ready : OwnPasswordState.NoPassword;
  }

  public async Task<OwnPasswordChangeResult> ChangeAsync(string currentPassword, string newPassword,
    string confirmation, CancellationToken ct = default)
  {
    var user = await GetUserAsync(ct);
    if (user is null) return new(OwnPasswordChangeStatus.Unauthorized);
    if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
      return new(OwnPasswordChangeStatus.Rejected, "Заполните текущий и новый пароль.");
    if (newPassword != confirmation)
      return new(OwnPasswordChangeStatus.Rejected, "Пароли не совпадают.");
    ct.ThrowIfCancellationRequested();
    var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
    if (!result.Succeeded)
      return new(OwnPasswordChangeStatus.Rejected, string.Join(" ", result.Errors.Select(e => e.Description)));
    // Do not interrupt cookie refresh after the password and security stamp were persisted.
    try { await signIn.RefreshSignInAsync(user); }
    catch (Exception ex) {
      logger.LogError(ex, "Пароль сохранён, но сеанс пользователя {UserId} не обновлён", user.Id);
      return new(OwnPasswordChangeStatus.SignInRequired);
    }
    return new(OwnPasswordChangeStatus.Succeeded);
  }
}
