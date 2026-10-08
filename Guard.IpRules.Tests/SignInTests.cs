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
  }

  private sealed class AuthenticationRecorder : IAuthenticationService
  {
    public int Issued { get; private set; }
    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());
    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
    { Issued++; return Task.CompletedTask; }
    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
  }
}
