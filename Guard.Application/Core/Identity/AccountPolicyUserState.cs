namespace Guard.Core.Identity;

public readonly record struct AccountPolicyUserState(
  DateTimeOffset CreatedAtUtc,
  DateTimeOffset? PasswordChangedAtUtc,
  DateTimeOffset? LastActivityAtUtc,
  DateTimeOffset? UnblockedAtUtc,
  bool MustChangePassword);
