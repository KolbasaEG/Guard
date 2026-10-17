using Guard.Core.Entities;

namespace Guard.Core.Identity;

internal static class AccountPolicyUserStateMapping
{
  internal static AccountPolicyUserState ToAccountPolicyState(this ApplicationUser user) => new(
    user.CreatedAtUtc, user.PasswordChangedAtUtc, user.LastActivityAtUtc,
    user.UnblockedAtUtc, user.MustChangePassword);
}
