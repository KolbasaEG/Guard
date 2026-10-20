using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Guard.Core.Identity;

public sealed class IpAccessDeniedException : Exception;

/// <summary>Единая проверка перед выдачей cookie для всех способов входа.</summary>
public class IpRestrictedSignInManager(
    UserManager<ApplicationUser> userManager, IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory, IOptions<IdentityOptions> options,
    ILogger<SignInManager<ApplicationUser>> logger, IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation, IIpAccessService access, IAuditService audit, IAccountPolicyService policies)
    : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory, options, logger, schemes, confirmation)
{
  private bool refreshing;
  internal string? LastFailureReason { get; private set; }
  public override async Task RefreshSignInAsync(ApplicationUser user)
  {
    refreshing = true;
    try { await base.RefreshSignInAsync(user); }
    finally { refreshing = false; }
  }
  protected override async Task<SignInResult?> PreSignInCheck(ApplicationUser user)
  {
    if (await UserManager.IsLockedOutAsync(user)) return SignInResult.LockedOut;
    return await base.PreSignInCheck(user);
  }

  public override async Task<SignInResult> CheckPasswordSignInAsync(ApplicationUser user, string password, bool lockoutOnFailure)
  {
    if (!await UserManager.GetLockoutEnabledAsync(user)) {
      var enabled = await UserManager.SetLockoutEnabledAsync(user, true);
      if (!enabled.Succeeded) return SignInResult.Failed;
    }
    // Every password entry point must count failures, including callers using the legacy false flag.
    return await base.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
  }

  public override async Task<bool> CanSignInAsync(ApplicationUser user)
  {
    if (!await base.CanSignInAsync(user)) return false;
    return await CheckIpAsync(user);
  }

  public override async Task SignInWithClaimsAsync(ApplicationUser user,
      AuthenticationProperties? authenticationProperties, IEnumerable<Claim> additionalClaims)
  {
    if (!await CheckIpAsync(user)) throw new IpAccessDeniedException();
    await audit.RecordAsync(AuditEventData.Create(refreshing ? Core.Enums.AuditEventType.SessionRefreshed : Core.Enums.AuditEventType.LoginSuccess, AuditOperationContext.Current?.UserId,
      ClientIpAddress.Get(Context), "Сервер разрешил выдачу сессии") with {
        ActorKind = AuditOperationContext.Current?.ActorKind ?? "anonymous", ObjectType = "Account", ObjectId = user.Id,
        AttemptedAccount = user.UserName, ReasonCode = refreshing ? "SessionRefreshed" : "SessionIssued"
      }, Context.RequestAborted);
    await base.SignInWithClaimsAsync(user, authenticationProperties, additionalClaims);
    await policies.RecordActivityAsync(user.Id, Context.RequestAborted);
  }

  private async Task<bool> CheckIpAsync(ApplicationUser user)
  {
    var state = await policies.CheckAsync(user.Id, Context.RequestAborted);
    if (state.Blocked) { LastFailureReason = "AccountBlocked"; return false; }
    var ip = ClientIpAddress.Get(Context);
    var allowed = await access.IsAllowedAsync(user.Id, ip, Context.RequestAborted);
    if (!allowed) LastFailureReason = "IpDenied";
    return allowed;
  }
}
