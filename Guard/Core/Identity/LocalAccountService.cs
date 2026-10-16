using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Identity;

namespace Guard.Core.Identity;

public sealed class LocalAccountService(IHttpContextAccessor contexts, UserManager<ApplicationUser> users,
  SignInManager<ApplicationUser> signIn, IAuditService audit) : ILocalAccountService
{
  public async Task<LocalSignInStatus> SignInAsync(string email, string password, bool rememberMe,
    CancellationToken ct = default)
  {
    ct.ThrowIfCancellationRequested();
    var context = contexts.HttpContext ?? throw new InvalidOperationException("Вход требует HTTP-запроса.");
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return LocalSignInStatus.Failed;
    email = email.Trim();
    // UserName может отличаться от email. Политики блокировки и IP остаются в Identity.
    var user = await users.FindByEmailAsync(email);
    ct.ThrowIfCancellationRequested();
    SignInResult result;
    if (user == null) result = SignInResult.Failed;
    else if (await users.GetTwoFactorEnabledAsync(user)) {
      // Не выдаём ни основную, ни промежуточную cookie, даже для запомненного устройства.
      result = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
      if (result.Succeeded) {
        audit.LogIdentityEvent(AuditEventType.LoginFailed, email, ClientIpAddress.Get(context), "Two-factor account requires administrator assistance");
        return LocalSignInStatus.RequiresTwoFactor;
      }
    } else result = await signIn.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);
    if (result.Succeeded) {
      audit.LogIdentityEvent(AuditEventType.LoginSuccess, email, ClientIpAddress.Get(context), "Successful login with password");
      return LocalSignInStatus.Succeeded;
    }
    if (result.IsNotAllowed) return LocalSignInStatus.NotAllowed;
    if (result.RequiresTwoFactor) return LocalSignInStatus.RequiresTwoFactor;
    audit.LogIdentityEvent(AuditEventType.LoginFailed, email, ClientIpAddress.Get(context),
      result.IsLockedOut ? "Account is locked out" : "Invalid email or password");
    return result.IsLockedOut ? LocalSignInStatus.LockedOut : LocalSignInStatus.Failed;
  }

  public async Task SignOutAsync(CancellationToken ct = default)
  {
    ct.ThrowIfCancellationRequested();
    var context = contexts.HttpContext ?? throw new InvalidOperationException("Выход требует HTTP-запроса.");
    var userName = context.User.Identity?.Name;
    var ip = ClientIpAddress.Get(context);
    await signIn.SignOutAsync();
    audit.LogIdentityEvent(AuditEventType.Logout, userName, ip, "User logged out");
  }
}
