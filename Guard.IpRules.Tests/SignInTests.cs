using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

internal static class SignInTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, IIpAccessService access, Action<bool, string> check)
  {
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton(factory);
    services.AddScoped(_ => factory.CreateDbContext());
    services.AddSingleton(access);
    services.AddSingleton<IAuditService, AuditStub>();
    services.AddSingleton<IAccessChangeNotifier, AccessChangeNotifier>();
    services.AddScoped<IAccountPolicyService, AccountPolicyService>();
    services.AddIdentity<ApplicationUser, ApplicationRole>().AddEntityFrameworkStores<ApplicationDbContext>()
        .AddUserManager<PolicyUserManager>()
        .AddSignInManager<IpRestrictedSignInManager>();
    var authentication = new AuthenticationRecorder();
    services.AddSingleton<IAuthenticationService>(authentication);
    await using var provider = services.BuildServiceProvider();
    await using var scope = provider.CreateAsyncScope();
    var context = MiddlewareTests.Context(false);
    context.RequestServices = scope.ServiceProvider;
    scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var manager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
    check(manager is IpRestrictedSignInManager, "DI uses restricted SignInManager");
    var user = (await users.FindByIdAsync("user"))!;
    var result = await users.AddPasswordAsync(user, "TestPassword123!");
    check(result.Succeeded, "test password setup");
    var denied = await manager.PasswordSignInAsync("user", "TestPassword123!", false, false);
    check(denied.IsNotAllowed && authentication.Issued == 0, "password login denied without cookie when assignments empty");
    await PostgresTests.ThrowsAsync<IpAccessDeniedException>(
        () => manager.SignInAsync(user, false), check, "direct sign-in denied before cookie");
    check(authentication.Issued == 0, "direct denial issued no cookie");
    var root = (await users.FindByIdAsync("root"))!;
    await manager.SignInAsync(root, false);
    check(authentication.Issued == 1, "Root direct sign-in issued cookie");
    // Назначение активного IP открывает обычный вход.
    await using var db = factory.CreateDbContext();
    var personal = await db.Personals.SingleAsync(p => p.User!.Id == "user");
    var ip = await db.IpAddresses.SingleAsync(ip => ip.Address == "192.168.1.0/24");
    personal.IpAddresses.Add(ip);
    await db.SaveChangesAsync();
    var success = await manager.PasswordSignInAsync("user", "TestPassword123!", false, false);
    check(success.Succeeded && authentication.Issued == 2, "allowed password login issues cookie");
    user.Email = "different-email@example.test";
    check((await users.UpdateAsync(user)).Succeeded, "email distinct from username is saved");
    check((await manager.PasswordSignInAsync(user.Email, "TestPassword123!", false, false)).Succeeded == false,
      "string login overload searches username rather than email");
    var byEmail = await users.FindByEmailAsync("DIFFERENT-EMAIL@example.test");
    check(byEmail?.Id == user.Id, "email lookup is case insensitive");
    check((await manager.PasswordSignInAsync(byEmail!, "TestPassword123!", false, false)).Succeeded,
      "email-resolved account signs in with its password");
    check(!(await manager.PasswordSignInAsync(byEmail!, "incorrect", false, false)).Succeeded,
      "email-resolved account still rejects incorrect password");
    context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.1");
    await PostgresTests.ThrowsAsync<IpAccessDeniedException>(
        () => manager.SignInWithClaimsAsync(user, new AuthenticationProperties(), [new Claim("amr", "mfa")]),
        check, "final sign-in gate rejects foreign IP with MFA claims");
    check(authentication.Issued == 3, "MFA claims cannot bypass IP gate");
    check((await manager.PasswordSignInAsync(byEmail!, "TestPassword123!", false, false)).IsNotAllowed,
      "email login preserves IP restriction");
    context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.10");
    check((await users.ResetAccessFailedCountAsync(user)).Succeeded, "prepare persisted login counter");
    var issuedBeforeFailures = authentication.Issued;
    for (var attempt = 1; attempt <= 5; attempt++) {
      var failure = await manager.PasswordSignInAsync(user, "incorrect", false, true);
      check(attempt == 5 ? failure.IsLockedOut : !failure.Succeeded && !failure.IsLockedOut,
        "persisted password failures reach lockout at configured threshold");
    }
    await using (var verify = await factory.CreateDbContextAsync()) {
      var persisted = await verify.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
      check(persisted.LockoutEnd > DateTimeOffset.UtcNow.AddMinutes(14) && persisted.AccessFailedCount == 0,
        "Identity stores temporary lockout and resets threshold counter in PostgreSQL");
    }
    check(authentication.Issued == issuedBeforeFailures, "password failures and lockout issue no cookie");
    check((await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddSeconds(-1))).Succeeded, "simulate elapsed temporary lockout");
    check((await manager.PasswordSignInAsync(user, "TestPassword123!", false, true)).Succeeded,
      "persisted expired lockout allows password login again");
    var accounts = new LocalAccountService(scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>(), users, manager,
      scope.ServiceProvider.GetRequiredService<IAuditService>());
    check(await accounts.SignInAsync("  DIFFERENT-EMAIL@example.test  ", "TestPassword123!", false) == Guard.Core.Services.DTOs.LocalSignInStatus.Succeeded,
      "local adapter resolves trimmed email distinct from username");
    check((await users.SetTwoFactorEnabledAsync(user, true)).Succeeded, "enable synthetic account 2FA");
    authentication.RememberedUserId = user.Id;
    check(await manager.IsTwoFactorClientRememberedAsync(user), "fixture authenticates remembered 2FA device");
    var beforeTwoFactor = authentication.Issued;
    check(await accounts.SignInAsync(user.Email!, "TestPassword123!", true) == Guard.Core.Services.DTOs.LocalSignInStatus.RequiresTwoFactor,
      "local adapter denies 2FA account even with remembered-device authentication");
    check(authentication.Issued == beforeTwoFactor, "2FA denial issues neither application nor intermediate cookie");
    check(await users.GetTwoFactorEnabledAsync(user), "2FA denial preserves stored protection");
    check(await accounts.SignInAsync(user.Email!, "incorrect", false) == Guard.Core.Services.DTOs.LocalSignInStatus.Failed,
      "2FA account still checks password and counts failures");
    check((await users.SetTwoFactorEnabledAsync(user, false)).Succeeded, "restore synthetic 2FA fixture");
    authentication.RememberedUserId = null;

  }

  private sealed class AuthenticationRecorder : IAuthenticationService
  {
    public int Issued { get; private set; }
    public string? RememberedUserId { get; set; }
    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(
      scheme == IdentityConstants.TwoFactorRememberMeScheme && RememberedUserId != null
        ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
          [new Claim(ClaimTypes.Name, RememberedUserId)], scheme)), scheme))
        : AuthenticateResult.NoResult());
    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
    { Issued++; return Task.CompletedTask; }
    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
  }
}
