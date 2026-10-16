using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Guard.Core.Identity;

public class PolicyUserManager(IUserStore<ApplicationUser> store, IOptions<IdentityOptions> options,
    IPasswordHasher<ApplicationUser> hasher, IEnumerable<IUserValidator<ApplicationUser>> users,
    IEnumerable<IPasswordValidator<ApplicationUser>> passwords, ILookupNormalizer normalizer,
    IdentityErrorDescriber errors, IServiceProvider services, ILogger<UserManager<ApplicationUser>> logger,
    IAuditService audit)
    : UserManager<ApplicationUser>(store, LocalOptions(options.Value), hasher, users, passwords, normalizer, errors, services, logger)
{
  // IOptions.Value is shared. Keep the dynamically changed Lockout options local to this manager.
  private static IOptions<IdentityOptions> LocalOptions(IdentityOptions shared) => Microsoft.Extensions.Options.Options.Create(new IdentityOptions {
    ClaimsIdentity = shared.ClaimsIdentity, User = shared.User, Password = shared.Password,
    SignIn = shared.SignIn, Tokens = shared.Tokens, Stores = shared.Stores,
    Lockout = new LockoutOptions {
      AllowedForNewUsers = shared.Lockout.AllowedForNewUsers,
      MaxFailedAccessAttempts = shared.Lockout.MaxFailedAccessAttempts,
      DefaultLockoutTimeSpan = shared.Lockout.DefaultLockoutTimeSpan
    }
  });

  public override async Task<IdentityResult> AccessFailedAsync(ApplicationUser user)
  {
    var ct = ServiceProvider.GetService<IHttpContextAccessor>()?.HttpContext?.RequestAborted ?? default;
    var policy = await ServiceProvider.GetRequiredService<IAccountPolicyService>().GetLoginPolicyAsync(ct);
    if (policy.MaxFailedLoginAttempts is < 1 or > 100 || policy.LoginLockoutMinutes is < 1 or > 10080) throw new InvalidOperationException("Некорректная политика блокировки входа.");
    Options.Lockout.MaxFailedAccessAttempts = policy.MaxFailedLoginAttempts;
    Options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(policy.LoginLockoutMinutes);
    var result = await base.AccessFailedAsync(user);
    if (result.Succeeded && user.LockoutEnd > DateTimeOffset.UtcNow)
      audit.LogIdentityEvent(Core.Enums.AuditEventType.UserBlocked, user.Id, details: "Failed login attempts; temporary lockout");
    return result;
  }

  private async Task<IdentityResult> SetPasswordAsync(ApplicationUser user, string password, Func<Task<IdentityResult>> change)
  {
    if (user.AccountBlockReason != null || await IsLockedOutAsync(user))
      return IdentityResult.Failed(new IdentityError { Code = "AccountBlocked", Description = "Учётная запись заблокирована." });
    if (user.PasswordHash != null && PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed)
      return IdentityResult.Failed(new IdentityError { Code = "PasswordUnchanged", Description = "Новый пароль должен отличаться от текущего." });
    var previous = (user.PasswordChangedAtUtc, user.MustChangePassword, user.PasswordHash, user.SecurityStamp);
    user.PasswordChangedAtUtc = DateTimeOffset.UtcNow;
    user.MustChangePassword = false;
    var succeeded = false;
    try {
      var result = await change();
      succeeded = result.Succeeded;
      return result;
    }
    finally {
      if (!succeeded) (user.PasswordChangedAtUtc, user.MustChangePassword, user.PasswordHash, user.SecurityStamp) = previous;
    }
  }

  public override Task<IdentityResult> CreateAsync(ApplicationUser user, string password) =>
    SetPasswordAsync(user, password, () => base.CreateAsync(user, password));

  public override async Task<IdentityResult> AddPasswordAsync(ApplicationUser user, string password)
  {
    var result = await SetPasswordAsync(user, password, () => base.AddPasswordAsync(user, password));
    if (result.Succeeded) audit.LogIdentityEvent(Core.Enums.AuditEventType.PasswordChanged, user.Id);
    return result;
  }

  public override async Task<IdentityResult> ResetPasswordAsync(ApplicationUser user, string token, string newPassword)
  {
    if (!await VerifyUserTokenAsync(user, Options.Tokens.PasswordResetTokenProvider, ResetPasswordTokenPurpose, token))
      return IdentityResult.Failed(ErrorDescriber.InvalidToken());
    return await SetPasswordAsync(user, newPassword, () => base.ResetPasswordAsync(user, token, newPassword));
  }

  public override async Task<IdentityResult> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword)
  {
    if (!await CheckPasswordAsync(user, currentPassword))
      return IdentityResult.Failed(new IdentityError { Code = "PasswordMismatch", Description = "Текущий пароль неверен." });
    var result = await SetPasswordAsync(user, newPassword, () => base.ChangePasswordAsync(user, currentPassword, newPassword));
    if (result.Succeeded) audit.LogIdentityEvent(Core.Enums.AuditEventType.PasswordChanged, user.Id);
    return result;
  }
}
