using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

internal static class LoginLockoutTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var policy = new AccountPolicy();
    check(policy.MaxFailedLoginAttempts == 5 && policy.LoginLockoutMinutes == 15, "default login policy is five failures and fifteen minutes");
    foreach (var attempts in new[] { 0, -1, 101 }) {
      policy.MaxFailedLoginAttempts = attempts; check(!AccountPolicyRules.Valid(policy), "invalid failed-login threshold rejected");
    }
    policy.MaxFailedLoginAttempts = 5;
    foreach (var minutes in new[] { 0, -1, 10081 }) {
      policy.LoginLockoutMinutes = minutes; check(!AccountPolicyRules.Valid(policy), "invalid lockout duration rejected");
    }
    policy.LoginLockoutMinutes = 15;
    await using (var nonRoot = new ServiceCollection().AddSingleton<IPermissionService, NonRootPermissions>().BuildServiceProvider()) {
      var service = new AccountPolicyService(new NoDatabaseFactory(), nonRoot, new AuditStub(),
        new AccessChangeNotifier(Microsoft.Extensions.Logging.Abstractions.NullLogger<AccessChangeNotifier>.Instance));
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync(new SaveAccountPolicyCommand { MaxFailedLoginAttempts = policy.MaxFailedLoginAttempts, LoginLockoutMinutes = policy.LoginLockoutMinutes }), check,
        "non-Root cannot change login threshold or duration before any database write");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetSettingsAsync(), check,
        "non-Root cannot read administrative settings before any database access");
    }
    using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options)) {
      check(!db.Database.HasPendingModelChanges(), "login policy migration matches EF model");
      var sql = db.GetService<IMigrator>().GenerateScript("20261008160000_AddEntityVersions", "20261009090000_AddLoginFailurePolicy");
      check(sql.Contains("DEFAULT 5") && sql.Contains("DEFAULT 15") && sql.Contains("CK_AccountPolicies_LoginLockout"), "migration preserves legacy policy with new defaults and constraint");
    }
    var user = new ApplicationUser { Id = "test", UserName = "test", NormalizedUserName = "TEST", SecurityStamp = "test", LockoutEnabled = false };
    user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, "CorrectPassword123!");
    var store = new Store(user);
    var access = new IpAccess();
    var policies = new Policies(policy, user);
    var authentication = new AuthenticationRecorder();
    var registrations = new ServiceCollection();
    registrations.AddLogging(); registrations.AddHttpContextAccessor(); registrations.AddAuthentication();
    registrations.AddSingleton<IAuthenticationService>(authentication);
    registrations.AddSingleton<IAuditService, AuditStub>();
    registrations.AddSingleton<IIpAccessService>(access);
    registrations.AddSingleton<IAccountPolicyService>(policies);
    registrations.AddIdentityCore<ApplicationUser>().AddUserManager<PolicyUserManager>().AddSignInManager<IpRestrictedSignInManager>();
    registrations.AddSingleton<IUserStore<ApplicationUser>>(store);
    await using var provider = registrations.BuildServiceProvider();
    await using var scope = provider.CreateAsyncScope();
    var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
    scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
    var manager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
    var shared = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;
    var initialDuration = shared.Lockout.DefaultLockoutTimeSpan;
    check(!(await manager.PasswordSignInAsync("missing", "incorrect", false, true)).Succeeded && user.AccessFailedCount == 0,
      "unknown account does not increment another account's counter");
    for (var attempt = 1; attempt < 5; attempt++) {
      var result = await manager.CheckPasswordSignInAsync(user, "incorrect", false);
      check(!result.Succeeded && !result.IsLockedOut && user.AccessFailedCount == attempt, "incorrect password counts even through legacy false flag");
    }
    var before = DateTimeOffset.UtcNow;
    check((await manager.CheckPasswordSignInAsync(user, "incorrect", true)).IsLockedOut, "fifth incorrect password locks account");
    check(user.LockoutEnd >= before.AddMinutes(15) && user.LockoutEnd <= DateTimeOffset.UtcNow.AddMinutes(15), "default lockout lasts fifteen minutes");
    var end = user.LockoutEnd;
    check((await manager.PasswordSignInAsync(user, "CorrectPassword123!", false, true)).IsLockedOut && user.LockoutEnd == end && authentication.Issued == 0,
      "correct password during lockout cannot issue cookie or extend timer");
    check(shared.Lockout.DefaultLockoutTimeSpan == initialDuration && shared.Lockout.MaxFailedAccessAttempts == 5, "dynamic policy does not mutate shared Identity options");
    user.LockoutEnd = DateTimeOffset.UtcNow.AddSeconds(-1);
    check((await manager.PasswordSignInAsync(user, "CorrectPassword123!", false, true)).Succeeded && authentication.Issued == 1,
      "expired temporary lockout allows cookie without administrator action");
    await manager.CheckPasswordSignInAsync(user, "incorrect", true);
    check((await manager.CheckPasswordSignInAsync(user, "CorrectPassword123!", true)).Succeeded && user.AccessFailedCount == 0, "successful password check resets previous failure count");
    policy.MaxFailedLoginAttempts = 2; policy.LoginLockoutMinutes = 30;
    await manager.CheckPasswordSignInAsync(user, "incorrect", true);
    before = DateTimeOffset.UtcNow;
    check((await manager.CheckPasswordSignInAsync(user, "incorrect", true)).IsLockedOut && user.LockoutEnd >= before.AddMinutes(30), "changed policy applies to subsequent failure with new threshold and duration");
    policy.LoginLockoutMinutes = 1;
    check((await manager.CheckPasswordSignInAsync(user, "incorrect", true)).IsLockedOut && user.LockoutEnd >= before.AddMinutes(30), "changing policy does not shorten an existing lockout");
    foreach (var reason in new[] { "Administrator", "Inactivity" }) {
      user.AccountBlockReason = reason; user.LockoutEnd = DateTimeOffset.MaxValue;
      check((await manager.CheckPasswordSignInAsync(user, "CorrectPassword123!", true)).IsLockedOut && user.AccountBlockReason == reason && user.LockoutEnd == DateTimeOffset.MaxValue,
        "password login cannot clear permanent account block");
    }
    user.AccountBlockReason = null; user.LockoutEnd = null; user.AccessFailedCount = 0;
    access.Allowed = false;
    check((await manager.CheckPasswordSignInAsync(user, "incorrect", true)).IsNotAllowed && user.AccessFailedCount == 0, "IP denial is not counted as an incorrect password");
    access.Allowed = true; user.AccessFailedCount = 1; store.FailNextUpdate = true;
    check(!(await manager.PasswordSignInAsync(user, "CorrectPassword123!", false, true)).Succeeded && authentication.Issued == 1,
      "failed Identity reset cannot produce successful authentication or cookie");
  }

  private sealed class IpAccess : IIpAccessService
  {
    public bool Allowed = true;
    public Task<bool> IsAllowedAsync(string id, string? ip, CancellationToken ct = default) => Task.FromResult(Allowed);
  }
  private sealed class NoDatabaseFactory : IDbContextFactory<ApplicationDbContext>
  {
    public ApplicationDbContext CreateDbContext() => throw new Exception("Unauthorized policy write reached database");
  }
  private sealed class NonRootPermissions : IPermissionService
  {
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(new UserAccessSnapshot("user", false, []));
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string code, CancellationToken ct = default) => Task.FromResult(false);
    public Task RequireAsync(string code, CancellationToken ct = default) => throw new UnauthorizedAccessException();
  }
  private sealed class AuthenticationRecorder : IAuthenticationService
  {
    public int Issued;
    public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(AuthenticateResult.NoResult());
    public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    public Task SignInAsync(HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal, AuthenticationProperties? properties) { Issued++; return Task.CompletedTask; }
    public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
  }
  private sealed class Policies(AccountPolicy policy, ApplicationUser user) : IAccountPolicyService
  {
    public Task<AccountLoginPolicyDto> GetLoginPolicyAsync(CancellationToken ct = default) => Task.FromResult(new AccountLoginPolicyDto(policy.MaxFailedLoginAttempts, policy.LoginLockoutMinutes));
    public Task<AccountPasswordLifetimeDto> GetPasswordLifetimeAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<AccountState> CheckAsync(string id, CancellationToken ct = default) => Task.FromResult(new AccountState(user.AccountBlockReason != null || user.LockoutEnd > DateTimeOffset.UtcNow, false));
    public Task RecordActivityAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
    public Task SweepAsync(CancellationToken ct = default) => throw new NotSupportedException();
  }
  private sealed class Store(ApplicationUser user) : IUserPasswordStore<ApplicationUser>, IUserLockoutStore<ApplicationUser>, IUserSecurityStampStore<ApplicationUser>
  {
    public bool FailNextUpdate;
    public void Dispose() { }
    public Task<string> GetUserIdAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.Id);
    public Task<string?> GetUserNameAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.UserName);
    public Task SetUserNameAsync(ApplicationUser u, string? value, CancellationToken ct) { u.UserName = value; return Task.CompletedTask; }
    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.NormalizedUserName);
    public Task SetNormalizedUserNameAsync(ApplicationUser u, string? value, CancellationToken ct) { u.NormalizedUserName = value; return Task.CompletedTask; }
    public Task<IdentityResult> CreateAsync(ApplicationUser u, CancellationToken ct) => throw new NotSupportedException();
    public Task<IdentityResult> DeleteAsync(ApplicationUser u, CancellationToken ct) => throw new NotSupportedException();
    public Task<IdentityResult> UpdateAsync(ApplicationUser u, CancellationToken ct) {
      if (!FailNextUpdate) return Task.FromResult(IdentityResult.Success);
      FailNextUpdate = false; return Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "TestFailure" }));
    }
    public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken ct) => Task.FromResult(id == user.Id ? user : null);
    public Task<ApplicationUser?> FindByNameAsync(string name, CancellationToken ct) => Task.FromResult(name == user.NormalizedUserName ? user : null);
    public Task SetPasswordHashAsync(ApplicationUser u, string? hash, CancellationToken ct) { u.PasswordHash = hash; return Task.CompletedTask; }
    public Task<string?> GetPasswordHashAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.PasswordHash);
    public Task<bool> HasPasswordAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.PasswordHash != null);
    public Task<DateTimeOffset?> GetLockoutEndDateAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.LockoutEnd);
    public Task SetLockoutEndDateAsync(ApplicationUser u, DateTimeOffset? date, CancellationToken ct) { u.LockoutEnd = date; return Task.CompletedTask; }
    public Task<int> IncrementAccessFailedCountAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(++u.AccessFailedCount);
    public Task ResetAccessFailedCountAsync(ApplicationUser u, CancellationToken ct) { u.AccessFailedCount = 0; return Task.CompletedTask; }
    public Task<int> GetAccessFailedCountAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.AccessFailedCount);
    public Task<bool> GetLockoutEnabledAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.LockoutEnabled);
    public Task SetLockoutEnabledAsync(ApplicationUser u, bool value, CancellationToken ct) { u.LockoutEnabled = value; return Task.CompletedTask; }
    public Task SetSecurityStampAsync(ApplicationUser u, string stamp, CancellationToken ct) { u.SecurityStamp = stamp; return Task.CompletedTask; }
    public Task<string?> GetSecurityStampAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.SecurityStamp);
  }
}
