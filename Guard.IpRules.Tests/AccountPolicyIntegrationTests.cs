using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Guard.Core.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

internal static class AccountPolicyIntegrationTests
{
  public static async Task RunAsync(IServiceProvider services, IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    var policies = services.GetRequiredService<IAccountPolicyService>();
    var security = services.GetRequiredService<ISecurityService>();
    await using var db = await factory.CreateDbContextAsync();
    var subdivision = await db.Subdivisions.FirstAsync();
    var personal = new Personal { FirstName = "Policy", LastName = "Test", SubdivisionId = subdivision.Id, CreatedBy = "test" };
    db.Add(personal); await db.SaveChangesAsync();
    var target = new ApplicationUser { UserName = "policy-user", Email = "policy@example.test", PersonalId = personal.Id };
    check((await security.CreateUserAsync(target, "InitialPassword123!")).Succeeded, "policy user created");
    await UserListTests.RunAsync(security, check);
    async Task<ApplicationUser> Read() {
      await using var read = await factory.CreateDbContextAsync();
      return await read.Users.AsNoTracking().SingleAsync(u => u.Id == target.Id);
    }
    var created = await Read();
    check(created.PasswordChangedAtUtc.HasValue && !created.MustChangePassword, "creation records password timestamp atomically");
    check((await security.ResetPasswordAsync(target.Id, "TemporaryPassword123!")).Succeeded, "admin password reset succeeds");
    var reset = await Read();
    check(reset.MustChangePassword && reset.SecurityStamp != created.SecurityStamp, "admin reset requires change and revokes previous stamp");
    check((await policies.CheckAsync(target.Id)).PasswordExpired, "temporary password blocks application access");
    await PostgresTests.ThrowsAsync<InvalidOperationException>(() => security.ResetPasswordAsync(target.Id, "TemporaryPassword123!"), check, "same password reset rejected");
    await using (var scope = services.CreateAsyncScope()) {
      var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
      var user = (await manager.FindByIdAsync(target.Id))!;
      check((await manager.ChangePasswordAsync(user, "TemporaryPassword123!", "PermanentPassword456!")).Succeeded, "user changes temporary password");
    }
    check(!(await Read()).MustChangePassword && !(await policies.CheckAsync(target.Id)).PasswordExpired, "self change clears mandatory change");
    await PostgresTests.ThrowsAsync<InvalidOperationException>(() => security.ToggleUserLockoutAsync("root", true), check, "Root cannot be blocked");
    check((await security.ToggleUserLockoutAsync(target.Id, true)).Succeeded, "manual block succeeds");
    check((await policies.CheckAsync(target.Id)).Blocked && (await Read()).AccountBlockReason == "Administrator", "manual block denies account");
    await PostgresTests.ThrowsAsync<InvalidOperationException>(() => security.ResetPasswordAsync(target.Id, "AnotherPassword789!"), check, "blocked account reset denied");
    check((await security.ToggleUserLockoutAsync(target.Id, false)).Succeeded, "manual unblock succeeds");
    var unblocked = await Read();
    check(unblocked.UnblockedAtUtc.HasValue && unblocked.AccountBlockReason == null, "unblock clears reason and restarts activity window");
    var first = await policies.GetAsync();
    first.InactivityEnabled = true; first.InactivityDays = 20;
    await policies.SaveAsync(first);
    var stale = await policies.GetAsync();
    var updated = await policies.GetAsync(); updated.MinimumLength = 12;
    await policies.SaveAsync(updated);
    await PostgresTests.ThrowsAsync<InvalidOperationException>(() => policies.SaveAsync(stale), check, "stale policy version rejected");
    check((await policies.GetAsync()).MinimumLength == 12, "stale policy cannot overwrite committed settings");
    var old = DateTimeOffset.UtcNow.AddDays(-40);
    updated = await policies.GetAsync(); updated.PasswordExpirationEnabled = true; updated.PasswordDays = 30;
    await policies.SaveAsync(updated);
    await db.Users.Where(u => u.Id == target.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordChangedAtUtc, old));
    check((await policies.CheckAsync(target.Id)).PasswordExpired, "N-day expiry enforced in database-backed policy");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => services.GetRequiredService<IPermissionService>().GetForUserAsync(target.Id), check, "expired password denies application permissions");
    await using (var middlewareScope = services.CreateAsyncScope()) {
      var manager = middlewareScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
      var user = (await manager.FindByIdAsync(target.Id))!;
      var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, target.Id),
        new Claim(manager.Options.ClaimsIdentity.SecurityStampClaimType, user.SecurityStamp!)], "test"));
      var reached = false;
      var middleware = new AccountPolicyMiddleware(_ => { reached = true; return Task.CompletedTask; });
      var context = new DefaultHttpContext { User = principal, RequestServices = middlewareScope.ServiceProvider };
      context.Request.Path = "/users";
      await middleware.InvokeAsync(context, policies, manager);
      check(!reached && context.Response.Headers.Location == "/Account/Manage/ChangePassword", "expired HTTP request redirects to password change");
      context = new DefaultHttpContext { User = principal, RequestServices = middlewareScope.ServiceProvider };
      context.Request.Path = "/Account/Manage/ChangePassword";
      await middleware.InvokeAsync(context, policies, manager);
      check(reached, "expired account can open password change without redirect loop");
      await PostgresTests.ThrowsAsync<ArgumentException>(() => policies.SaveAsync(new AccountPolicy { Id = 2 }), check, "invalid policy singleton rejected");
      principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "root"),
        new Claim(manager.Options.ClaimsIdentity.SecurityStampClaimType, "revoked")], "test"));
      var fixedPermissions = new PermissionService(new FixedAuthentication(principal), new HttpContextAccessor(), factory, new AuditStub(), policies);
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => fixedPermissions.GetCurrentAsync(), check, "revoked stamp cannot use application services");
    }
    updated = await policies.GetAsync(); updated.PasswordExpirationEnabled = false;
    await policies.SaveAsync(updated);
    var currentTarget = await Read();
    var targetPrincipal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, target.Id),
      new Claim("AspNet.Identity.SecurityStamp", currentTarget.SecurityStamp!)], "test"));
    var nonRootPermissions = new PermissionService(new FixedAuthentication(targetPrincipal), new HttpContextAccessor(), factory, new AuditStub(), policies);
    await using (var nonRootProvider = new ServiceCollection().AddSingleton<IPermissionService>(nonRootPermissions).BuildServiceProvider()) {
      var nonRootPolicy = new AccountPolicyService(factory, nonRootProvider, new AuditStub(), services.GetRequiredService<IAccessChangeNotifier>());
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => nonRootPolicy.SaveAsync(updated), check, "non-Root cannot change account policy");
    }
    await db.Set<AccountPolicy>().ExecuteUpdateAsync(s => s.SetProperty(p => p.InactivityEnabledAtUtc, old));
    await db.Users.Where(u => u.Id == target.Id).ExecuteUpdateAsync(s => s
      .SetProperty(u => u.CreatedAtUtc, old).SetProperty(u => u.LastActivityAtUtc, old).SetProperty(u => u.UnblockedAtUtc, (DateTimeOffset?)null));
    await policies.SweepAsync();
    var inactive = await Read();
    check(inactive.AccountBlockReason == "Inactivity" && inactive.SecurityStamp != unblocked.SecurityStamp,
      "background inactivity block changes stamp and reason");
    await security.ToggleUserLockoutAsync(target.Id, false);
    check(!(await policies.CheckAsync(target.Id)).Blocked, "unblocked inactive account is not immediately blocked again");
    await policies.RecordActivityAsync(target.Id);
    check((await Read()).LastActivityAtUtc > old, "activity persisted");
    var recorded = (await Read()).LastActivityAtUtc;
    await policies.RecordActivityAsync(target.Id);
    check((await Read()).LastActivityAtUtc == recorded, "activity writes throttled");
    await db.Users.Where(u => u.Id == "root").ExecuteUpdateAsync(s => s.SetProperty(u => u.CreatedAtUtc, old).SetProperty(u => u.LastActivityAtUtc, old));
    check(!(await policies.CheckAsync("root")).Blocked, "inactive Root retains access");
    var policy = await policies.GetAsync(); policy.InactivityEnabled = false;
    await policies.SaveAsync(policy);
  }
  private sealed class FixedAuthentication(ClaimsPrincipal user) : AuthenticationStateProvider {
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(user));
  }
}
