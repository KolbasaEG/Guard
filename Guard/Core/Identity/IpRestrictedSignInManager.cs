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
    IUserConfirmation<ApplicationUser> confirmation, IIpAccessService access, IAuditService audit)
    : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory, options, logger, schemes, confirmation)
{
  public override async Task<bool> CanSignInAsync(ApplicationUser user)
  {
    if (!await base.CanSignInAsync(user)) return false;
    return await CheckIpAsync(user);
  }

  public override async Task SignInWithClaimsAsync(ApplicationUser user,
      AuthenticationProperties? authenticationProperties, IEnumerable<Claim> additionalClaims)
  {
    if (!await CheckIpAsync(user)) throw new IpAccessDeniedException();
    await base.SignInWithClaimsAsync(user, authenticationProperties, additionalClaims);
  }

  private async Task<bool> CheckIpAsync(ApplicationUser user)
  {
    var ip = ClientIpAddress.Get(Context);
    var allowed = await access.IsAllowedAsync(user.Id, ip, Context.RequestAborted);
    if (!allowed)
      audit.LogIdentityEvent(Core.Enums.AuditEventType.LoginFailed, user.UserName, ip, "IP access denied");
    return allowed;
  }
}
