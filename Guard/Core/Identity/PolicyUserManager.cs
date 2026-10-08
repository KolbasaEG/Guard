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
    : UserManager<ApplicationUser>(store, options, hasher, users, passwords, normalizer, errors, services, logger)
{
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
