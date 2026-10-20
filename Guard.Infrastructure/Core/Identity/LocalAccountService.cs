using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Guard.Core.Identity;

public sealed class LocalAccountService(IHttpContextAccessor contexts, UserManager<ApplicationUser> users,
  SignInManager<ApplicationUser> signIn, IAuditService audit, IOperationAvailability? availability = null) : ILocalAccountService
{
  private static readonly PartitionedRateLimiter<string> RecoveryLimiter = PartitionedRateLimiter.Create<string, string>(ip =>
    RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions {
      PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
    }));
  public async Task<LocalSignInStatus> SignInAsync(string email, string password, bool rememberMe,
    CancellationToken ct = default)
  {
    try { return await SignInCoreAsync(email, password, rememberMe, ct); }
    catch (Exception error) when (error is System.Data.Common.DbException or TimeoutException) { return LocalSignInStatus.AuditUnavailable; }
  }
  private async Task<LocalSignInStatus> SignInCoreAsync(string email, string password, bool rememberMe, CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();
    var context = contexts.HttpContext ?? throw new InvalidOperationException("Вход требует HTTP-запроса.");
    if (availability != null) {
      try { await availability.RequireWorkingAsync(ct); }
      catch (AuditUnavailableException) {
        try { return await RecoveryAsync(email, password, context, ct); }
        catch (Exception error) when (error is System.Data.Common.DbException or TimeoutException) { return LocalSignInStatus.AuditUnavailable; }
      }
    }
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password) || email.Length > 256) {
      await RecordRejectedSignInAsync(email, ct); return LocalSignInStatus.Failed;
    }
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
        await FailedAsync("UnsupportedTwoFactor", user);
        return LocalSignInStatus.RequiresTwoFactor;
      }
    } else result = await signIn.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);
    if (result.Succeeded) {
      // Производственный SignInManager сохраняет событие до выдачи cookie.
      if (signIn is not IpRestrictedSignInManager)
        await audit.RecordAsync(AuditEventData.ForAccount(AuditEventType.LoginSuccess, user?.Id, ClientIpAddress.Get(context)), ct);
      return LocalSignInStatus.Succeeded;
    }
    await FailedAsync(result.IsLockedOut ? "AccountLockedOut" : (signIn as IpRestrictedSignInManager)?.LastFailureReason
      ?? (result.IsNotAllowed ? "NotAllowed" : result.RequiresTwoFactor ? "RequiresTwoFactor" : user == null ? "UnknownAccount" : "InvalidCredentials"), user);
    if (result.IsNotAllowed) return LocalSignInStatus.NotAllowed;
    if (result.RequiresTwoFactor) return LocalSignInStatus.RequiresTwoFactor;
    return result.IsLockedOut ? LocalSignInStatus.LockedOut : LocalSignInStatus.Failed;

    Task FailedAsync(string reason, ApplicationUser? target) => audit.RecordAsync(
      AuditEventData.ForAccount(AuditEventType.LoginFailed, target?.Id, ClientIpAddress.Get(context)) with {
        Outcome = "failure", AttemptedAccount = email, ReasonCode = reason
      }, ct);
  }

  public Task RecordRejectedSignInAsync(string? attemptedAccount, CancellationToken ct = default)
  {
    var context = contexts.HttpContext ?? throw new InvalidOperationException("Вход требует HTTP-запроса.");
    return audit.RecordAsync(AuditEventData.ForAccount(AuditEventType.LoginFailed, null, ClientIpAddress.Get(context))
      with { Outcome = "failure", ReasonCode = "InvalidInput", AttemptedAccount = attemptedAccount?.Trim() }, ct);
  }

  private async Task<LocalSignInStatus> RecoveryAsync(string email, string password, HttpContext context, CancellationToken ct)
  {
    using var admission = await RecoveryLimiter.AcquireAsync(ClientIpAddress.Get(context) ?? "unknown", cancellationToken: ct);
    if (!admission.IsAcquired) return LocalSignInStatus.AuditUnavailable;
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return LocalSignInStatus.AuditUnavailable;
    var user = await users.FindByEmailAsync(email.Trim());
    if (user?.PasswordHash == null || user.AccountBlockReason != null || user.LockoutEnd > DateTimeOffset.UtcNow ||
        await users.GetTwoFactorEnabledAsync(user) || !await users.IsInRoleAsync(user, "Root") ||
        users.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
      return LocalSignInStatus.AuditUnavailable;
    ct.ThrowIfCancellationRequested();
    var principal = await signIn.CreateUserPrincipalAsync(user);
    ((ClaimsIdentity)principal.Identity!).AddClaim(new("guard:audit-recovery", "1"));
    await context.SignInAsync(IdentityConstants.ApplicationScheme, principal,
      new AuthenticationProperties { IsPersistent = false, AllowRefresh = false, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(15) });
    try { await audit.RecordAsync(AuditEventData.ForAccount(AuditEventType.AuditStateChanged, user.Id)
      with { Actor = user.Id, ActorKind = "user", ReasonCode = "RecoverySignIn", Details = "Вход только в диагностику аудита" }, ct); }
    catch (AuditUnavailableException) { /* Восстановление разрешено при отказе локального журнала. */ }
    return LocalSignInStatus.RecoveryOnly;
  }

  public async Task SignOutAsync(CancellationToken ct = default)
  {
    ct.ThrowIfCancellationRequested();
    var context = contexts.HttpContext ?? throw new InvalidOperationException("Выход требует HTTP-запроса.");
    var userName = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    var ip = ClientIpAddress.Get(context);
    await signIn.SignOutAsync();
    try { await audit.RecordAsync(AuditEventData.ForAccount(AuditEventType.Logout, userName, ip, "User logged out"), ct); }
    catch (Exception error) when (error is AuditUnavailableException or IOException or OperationCanceledException) { /* Выход не блокируется аудитом. */ }
  }
}
