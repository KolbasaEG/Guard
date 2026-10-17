using Guard.Core.Entities;
using Guard.Core.Identity;

internal static class AccountPolicyRuleTests
{
  public static void Run(Action<bool, string> check)
  {
    var origin = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    DateTimeOffset?[] optional = [null, origin.AddDays(-10), origin.AddDays(10)];
    foreach (var created in new[] { origin.AddDays(-20), origin.AddDays(20) })
    foreach (var changed in optional)
    foreach (var activated in optional)
    foreach (var enabled in new[] { false, true })
    foreach (var mustChange in new[] { false, true })
    {
      var policy = new AccountPolicy { PasswordExpirationEnabled = enabled, PasswordDays = 30, PasswordEnabledAtUtc = activated };
      var state = new AccountPolicyUserState(created, changed, null, null, mustChange);
      // A recorded password change takes precedence over policy activation.
      var start = changed ?? new[] { created, activated ?? created }.Max();
      var deadline = start.AddDays(30);
      foreach (var delta in new[] { -1L, 0L, 1L })
      {
        var now = deadline.AddTicks(delta);
        var expected = mustChange || enabled && delta >= 0;
        check(AccountPolicyRules.PasswordExpired(policy, state, now) == expected,
          "password expiry preserves explicit change, legacy activation and exact deadline");
        check(AccountPolicyRules.PasswordExpired(policy, state, now.ToOffset(TimeSpan.FromHours(3))) == expected,
          "password expiry compares instants regardless of offset");
      }
    }

    foreach (var created in new[] { origin.AddDays(-20), origin.AddDays(20) })
    foreach (var activity in optional)
    foreach (var unblocked in optional)
    foreach (var activated in optional)
    foreach (var enabled in new[] { false, true })
    foreach (var root in new[] { false, true })
    {
      var policy = new AccountPolicy { InactivityEnabled = enabled, InactivityDays = 20, InactivityEnabledAtUtc = activated };
      var state = new AccountPolicyUserState(created, null, activity, unblocked, false);
      // Creation is a fallback only when activity is absent.
      var start = new[] { activity ?? created, unblocked ?? DateTimeOffset.MinValue,
        activated ?? DateTimeOffset.MinValue }.Max();
      var deadline = start.AddDays(20);
      foreach (var delta in new[] { -1L, 0L, 1L })
      {
        var now = deadline.AddTicks(delta);
        var expected = !root && enabled && delta >= 0;
        check(AccountPolicyRules.Inactive(policy, state, root, now) == expected,
          "inactivity preserves activity fallback, unblock, activation, Root and exact deadline");
        check(AccountPolicyRules.Inactive(policy, state, root, now.ToOffset(TimeSpan.FromHours(-4))) == expected,
          "inactivity compares instants regardless of offset");
      }
    }

    foreach (var days in new[] { -1, 0, 1, 3650, 3651 })
    {
      check(AccountPolicyRules.Valid(new AccountPolicy { PasswordDays = days }) == (days is >= 1 and <= 3650), "password period validation boundaries");
      check(AccountPolicyRules.Valid(new AccountPolicy { InactivityDays = days }) == (days is >= 1 and <= 3650), "inactivity period validation boundaries");
    }
    foreach (var attempts in new[] { 0, 1, 100, 101 })
      check(AccountPolicyRules.Valid(new AccountPolicy { MaxFailedLoginAttempts = attempts }) == (attempts is >= 1 and <= 100), "login threshold validation boundaries");
    foreach (var minutes in new[] { 0, 1, 10080, 10081 })
      check(AccountPolicyRules.Valid(new AccountPolicy { LoginLockoutMinutes = minutes }) == (minutes is >= 1 and <= 10080), "lockout period validation boundaries");
    foreach (var length in new[] { 5, 6, 100, 101 })
    foreach (var unique in new[] { 0, 1, length, length + 1 })
      check(AccountPolicyRules.Valid(new AccountPolicy { MinimumLength = length, UniqueCharacters = unique }) ==
        (length is >= 6 and <= 100 && unique >= 1 && unique <= length), "password length and unique count validation boundaries");
    foreach (var id in new[] { 0, 1, 2 })
      check(AccountPolicyRules.Valid(new AccountPolicy { Id = id }) == (id == 1), "singleton policy ID validation");
  }
}
