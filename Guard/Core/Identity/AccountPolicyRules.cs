using Guard.Core.Entities;

namespace Guard.Core.Identity;

public static class AccountPolicyRules
{
  public static bool PasswordExpired(AccountPolicy policy, ApplicationUser user, DateTimeOffset now) =>
    user.MustChangePassword || (policy.PasswordExpirationEnabled &&
      (user.PasswordChangedAtUtc ?? Later(user.CreatedAtUtc, policy.PasswordEnabledAtUtc)).AddDays(policy.PasswordDays) <= now);

  public static bool Inactive(AccountPolicy policy, ApplicationUser user, bool root, DateTimeOffset now) =>
    !root && policy.InactivityEnabled &&
      Later(Later(user.LastActivityAtUtc ?? user.CreatedAtUtc, user.UnblockedAtUtc), policy.InactivityEnabledAtUtc)
        .AddDays(policy.InactivityDays) <= now;

  private static DateTimeOffset Later(DateTimeOffset value, DateTimeOffset? other) => other > value ? other.Value : value;

  public static bool Valid(AccountPolicy policy) => policy.Id == 1 &&
    policy.PasswordDays is >= 1 and <= 3650 && policy.InactivityDays is >= 1 and <= 3650 &&
    policy.MinimumLength is >= 6 and <= 100 && policy.UniqueCharacters >= 1 && policy.UniqueCharacters <= policy.MinimumLength;
}
