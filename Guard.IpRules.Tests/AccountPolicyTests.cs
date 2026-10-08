using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Contexts;
using Microsoft.EntityFrameworkCore;

internal static class AccountPolicyTests
{
  public static void Run(Action<bool, string> check)
  {
    using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql("Host=localhost;Database=design_only").Options);
    check(!db.Database.HasPendingModelChanges(), "migration snapshot matches current EF model");
    var now = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    var policy = new AccountPolicy { PasswordExpirationEnabled = true, PasswordDays = 30,
      PasswordEnabledAtUtc = now.AddDays(-10), InactivityEnabled = true, InactivityDays = 20, InactivityEnabledAtUtc = now.AddDays(-40) };
    var user = new ApplicationUser { CreatedAtUtc = now.AddYears(-1), PasswordChangedAtUtc = now.AddDays(-30), LastActivityAtUtc = now.AddDays(-20) };
    check(AccountPolicyRules.PasswordExpired(policy, user, now), "password expires at exact N-day boundary");
    check(!AccountPolicyRules.PasswordExpired(policy, user, now.AddTicks(-1)), "password valid before boundary");
    check(AccountPolicyRules.Inactive(policy, user, false, now), "inactivity blocks at exact M-day boundary");
    check(!AccountPolicyRules.Inactive(policy, user, false, now.AddTicks(-1)), "activity window valid before boundary");
    check(!AccountPolicyRules.Inactive(policy, user, true, now), "Root exempt from inactivity");
    check(AccountPolicyRules.PasswordExpired(policy, user, now), "Root password has no exemption in shared rule");
    user.PasswordChangedAtUtc = null;
    check(!AccountPolicyRules.PasswordExpired(policy, user, now), "legacy password gets full initial window");
    user.LastActivityAtUtc = null;
    policy.InactivityEnabledAtUtc = now;
    check(!AccountPolicyRules.Inactive(policy, user, false, now), "legacy inactivity starts on enable");
    policy.InactivityEnabledAtUtc = now.AddDays(-100);
    user.UnblockedAtUtc = now;
    check(!AccountPolicyRules.Inactive(policy, user, false, now), "unblock restarts inactivity period");
    user.CreatedAtUtc = now;
    user.UnblockedAtUtc = null;
    check(!AccountPolicyRules.Inactive(policy, user, false, now), "new account window starts on creation");
    policy.PasswordExpirationEnabled = false;
    user.MustChangePassword = true;
    check(AccountPolicyRules.PasswordExpired(policy, user, now), "temporary password requires change even with expiry disabled");
    user.MustChangePassword = false;
    check(!AccountPolicyRules.PasswordExpired(policy, user, now), "expiry disabled");
    policy.InactivityEnabled = false;
    user.CreatedAtUtc = now.AddYears(-1);
    check(!AccountPolicyRules.Inactive(policy, user, false, now), "inactivity disabled");
    policy = new AccountPolicy { MinimumLength = 12, UniqueCharacters = 6 };
    check(PolicyPasswordValidator.Validate(policy, "LongPassword12!").Succeeded, "all complexity rules accept strong password");
    foreach (var weak in new[] { "Aa1!", "longpassword12!", "LONGPASSWORD12!", "LongPassword!!!", "LongPassword123", new string('a', 101) })
      check(!PolicyPasswordValidator.Validate(policy, weak).Succeeded, "complexity rejects missing requirement");
    policy.RequireDigit = policy.RequireLowercase = policy.RequireUppercase = policy.RequireSymbol = false;
    check(PolicyPasswordValidator.Validate(policy, "differentletters").Succeeded, "complexity switches can be disabled");
    check(!PolicyPasswordValidator.Validate(policy, "aaaaaaaaaaaa").Succeeded, "unique character requirement enforced");
    foreach (var days in new[] { 0, -1, 3651 }) {
      policy.PasswordDays = days; check(!AccountPolicyRules.Valid(policy), "invalid N rejected");
      policy.PasswordDays = 30; policy.InactivityDays = days; check(!AccountPolicyRules.Valid(policy), "invalid M rejected");
      policy.InactivityDays = 30;
    }
    policy.UniqueCharacters = policy.MinimumLength + 1;
    check(!AccountPolicyRules.Valid(policy), "impossible unique count rejected");
  }
}
