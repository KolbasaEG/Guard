using System.Security.Claims;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal static class OwnPasswordCompletionTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    foreach (var failure in new[] { "none", "cookie", "rejected", "unknown", "unknown-commit", "cancelled-cookie" }) {
      var user = new ApplicationUser { Id = "password-user", UserName = "password-user", NormalizedUserName = "PASSWORD-USER",
        SecurityStamp = "old-stamp", MustChangePassword = true, PasswordChangedAtUtc = DateTimeOffset.UtcNow.AddDays(-100) };
      var hasher = new PasswordHasher<ApplicationUser>();
      user.PasswordHash = hasher.HashPassword(user, "OldPassword123!");
      var store = new Store(user) { Failure = failure };
      var audit = new Audit { Fail = failure is "audit" or "both" };
      var registrations = new ServiceCollection().AddLogging();
      registrations.AddHttpContextAccessor(); registrations.AddAuthentication();
      registrations.AddSingleton<IAuditService>(audit);
      registrations.AddIdentityCore<ApplicationUser>().AddUserManager<PolicyUserManager>().AddSignInManager<RefreshManager>();
      registrations.AddSingleton<IUserStore<ApplicationUser>>(store);
      registrations.AddScoped<IOwnPasswordService, OwnPasswordService>();
      await using var provider = registrations.BuildServiceProvider();
      await using var scope = provider.CreateAsyncScope();
      var services = scope.ServiceProvider;
      services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext {
        RequestServices = services,
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)], "test"))
      };
      var refresh = (RefreshManager)services.GetRequiredService<SignInManager<ApplicationUser>>();
      refresh.Failure = failure;
      var service = services.GetRequiredService<IOwnPasswordService>();
      var originalHash = store.Saved.PasswordHash;
      var originalDate = store.Saved.PasswordChangedAtUtc;
      var result = await service.ChangeAsync("wrong", "NewPassword123!", "NewPassword123!");
      check(result.Status == OwnPasswordChangeStatus.Rejected && store.Writes == 0 && refresh.Calls == 0 && audit.Calls == 1,
        $"{failure}: wrong password emits a refusal without writing or refreshing");
      result = await service.ChangeAsync("OldPassword123!", "NewPassword123!", "different");
      check(result.Status == OwnPasswordChangeStatus.Rejected && store.Writes == 0, $"{failure}: confirmation rejected before write");
      result = await service.ChangeAsync("OldPassword123!", "weak", "weak");
      check(result.Status == OwnPasswordChangeStatus.Rejected && store.Writes == 0 &&
        user.PasswordHash == originalHash && user.MustChangePassword && user.PasswordChangedAtUtc == originalDate,
        $"{failure}: validation failure restores tracked policy fields and password");
      if (failure is "unknown" or "unknown-commit") {
        await PostgresTests.ThrowsAsync<IOException>(() => service.ChangeAsync("OldPassword123!", "NewPassword123!", "NewPassword123!"), check,
          "store exception is not reported as confirmed password change");
      } else {
        result = await service.ChangeAsync("OldPassword123!", "NewPassword123!", "NewPassword123!");
        var expected = failure == "rejected" ? OwnPasswordChangeStatus.Rejected :
          failure is "cookie" or "both" or "cancelled-cookie" ? OwnPasswordChangeStatus.SignInRequired : OwnPasswordChangeStatus.Succeeded;
        check(result.Status == expected, $"{failure}: precise completion status");
      }
      var saved = store.Saved;
      if (failure == "unknown-commit") {
        check(store.Writes == 1 && saved.PasswordHash != originalHash && saved.SecurityStamp != "old-stamp" &&
          refresh.Calls == 0 && audit.Calls == 3, "lost store acknowledgement can persist password without claiming confirmed completion");
      } else if (failure is "rejected" or "unknown") {
        check(saved.PasswordHash == originalHash && saved.SecurityStamp == "old-stamp" && saved.MustChangePassword &&
          saved.PasswordChangedAtUtc == originalDate && refresh.Calls == 0 && audit.Calls == (failure == "rejected" ? 4 : 3),
          $"{failure}: unsuccessful store cannot produce cookie or success audit");
      } else {
        check(store.Writes == 1 && refresh.Calls == 1 && audit.Calls == 4, $"{failure}: no retry of password write or post-write actions");
        check(hasher.VerifyHashedPassword(saved, saved.PasswordHash!, "NewPassword123!") != PasswordVerificationResult.Failed &&
          hasher.VerifyHashedPassword(saved, saved.PasswordHash!, "OldPassword123!") == PasswordVerificationResult.Failed,
          $"{failure}: persisted password survives a later cookie failure");
        check(saved.SecurityStamp != "old-stamp" && !saved.MustChangePassword && saved.PasswordChangedAtUtc > originalDate &&
          saved.PasswordChangedAtUtc?.Offset == TimeSpan.Zero, $"{failure}: stamp and UTC policy fields persist with password");
      }
    }
  }

  private sealed class Audit : IAuditService
  {
    public bool Fail; public int Calls;
    public Task RecordAsync(Guard.Core.Services.AuditEventData entry, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); LogIdentityEvent(entry.EventType, entry.Actor, entry.ClientIp, entry.Details); return Task.CompletedTask; }
    public void LogIdentityEvent(Guard.Core.Enums.AuditEventType eventType, string? userName, string? ipAddress = null, string? details = null)
    {
      Calls++;
      if (Fail) throw new IOException("Synthetic audit delivery failure");
    }
  }

  private sealed class RefreshManager(UserManager<ApplicationUser> users, IHttpContextAccessor context,
    IUserClaimsPrincipalFactory<ApplicationUser> claims, IOptions<IdentityOptions> options,
    ILogger<SignInManager<ApplicationUser>> logger, IAuthenticationSchemeProvider schemes, IUserConfirmation<ApplicationUser> confirmation)
    : SignInManager<ApplicationUser>(users, context, claims, options, logger, schemes, confirmation)
  {
    public string Failure = "none"; public int Calls;
    public override Task RefreshSignInAsync(ApplicationUser user)
    {
      Calls++;
      if (Failure == "cancelled-cookie") throw new OperationCanceledException("Synthetic cookie cancellation after write");
      if (Failure is "cookie" or "both") throw new IOException("Synthetic cookie delivery failure");
      return Task.CompletedTask;
    }
  }

  // Keep a detached persisted copy: mutations of the tracked user do not prove that a write succeeded.
  private sealed class Store(ApplicationUser user) : IUserPasswordStore<ApplicationUser>, IUserSecurityStampStore<ApplicationUser>, IUserLockoutStore<ApplicationUser>
  {
    public string Failure = "none"; public int Writes;
    public ApplicationUser Saved { get; private set; } = Copy(user);
    private static ApplicationUser Copy(ApplicationUser u) => new() { Id = u.Id, PasswordHash = u.PasswordHash,
      SecurityStamp = u.SecurityStamp, MustChangePassword = u.MustChangePassword, PasswordChangedAtUtc = u.PasswordChangedAtUtc };
    public void Dispose() { }
    public Task<string> GetUserIdAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.Id);
    public Task<string?> GetUserNameAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.UserName);
    public Task SetUserNameAsync(ApplicationUser u, string? value, CancellationToken ct) { u.UserName = value; return Task.CompletedTask; }
    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.NormalizedUserName);
    public Task SetNormalizedUserNameAsync(ApplicationUser u, string? value, CancellationToken ct) { u.NormalizedUserName = value; return Task.CompletedTask; }
    public Task<IdentityResult> CreateAsync(ApplicationUser u, CancellationToken ct) => throw new NotSupportedException();
    public Task<IdentityResult> DeleteAsync(ApplicationUser u, CancellationToken ct) => throw new NotSupportedException();
    public Task<IdentityResult> UpdateAsync(ApplicationUser u, CancellationToken ct)
    {
      if (Failure == "unknown") throw new IOException("Synthetic store failure");
      if (Failure == "rejected") return Task.FromResult(IdentityResult.Failed(new IdentityError { Description = "Synthetic concurrency failure" }));
      Writes++; Saved = Copy(u);
      if (Failure == "unknown-commit") throw new IOException("Synthetic lost write acknowledgement");
      return Task.FromResult(IdentityResult.Success);
    }
    public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken ct) => Task.FromResult(id == user.Id ? user : null);
    public Task<ApplicationUser?> FindByNameAsync(string name, CancellationToken ct) => Task.FromResult(name == user.NormalizedUserName ? user : null);
    public Task SetPasswordHashAsync(ApplicationUser u, string? hash, CancellationToken ct) { u.PasswordHash = hash; return Task.CompletedTask; }
    public Task<string?> GetPasswordHashAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.PasswordHash);
    public Task<bool> HasPasswordAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.PasswordHash != null);
    public Task SetSecurityStampAsync(ApplicationUser u, string stamp, CancellationToken ct) { u.SecurityStamp = stamp; return Task.CompletedTask; }
    public Task<string?> GetSecurityStampAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.SecurityStamp);
    public Task<DateTimeOffset?> GetLockoutEndDateAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.LockoutEnd);
    public Task SetLockoutEndDateAsync(ApplicationUser u, DateTimeOffset? date, CancellationToken ct) { u.LockoutEnd = date; return Task.CompletedTask; }
    public Task<int> IncrementAccessFailedCountAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(++u.AccessFailedCount);
    public Task ResetAccessFailedCountAsync(ApplicationUser u, CancellationToken ct) { u.AccessFailedCount = 0; return Task.CompletedTask; }
    public Task<int> GetAccessFailedCountAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.AccessFailedCount);
    public Task<bool> GetLockoutEnabledAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.LockoutEnabled);
    public Task SetLockoutEnabledAsync(ApplicationUser u, bool value, CancellationToken ct) { u.LockoutEnabled = value; return Task.CompletedTask; }
  }
}
