namespace Guard.Core.Services.DTOs;

public sealed record PasswordRequirementsDto(int MinimumLength = 6, int UniqueCharacters = 1,
  bool RequireDigit = true, bool RequireLowercase = true, bool RequireUppercase = true, bool RequireSymbol = true);

public sealed record AccountLoginPolicyDto(int MaxFailedLoginAttempts = 5, int LoginLockoutMinutes = 15);
public sealed record AccountPasswordLifetimeDto(bool PasswordExpirationEnabled = false, int PasswordDays = 90,
  DateTimeOffset? PasswordEnabledAtUtc = null);

public sealed record SaveAccountPolicyCommand
{
  public Guid Version { get; init; }
  public int MaxFailedLoginAttempts { get; init; } = 5;
  public int LoginLockoutMinutes { get; init; } = 15;
  public bool PasswordExpirationEnabled { get; init; }
  public int PasswordDays { get; init; } = 90;
  public bool InactivityEnabled { get; init; }
  public int InactivityDays { get; init; } = 90;
  public int MinimumLength { get; init; } = 6;
  public int UniqueCharacters { get; init; } = 1;
  public bool RequireDigit { get; init; } = true;
  public bool RequireLowercase { get; init; } = true;
  public bool RequireUppercase { get; init; } = true;
  public bool RequireSymbol { get; init; } = true;
}

public sealed record AccountPolicySettingsDto
{
  public Guid Version { get; init; }
  public int MaxFailedLoginAttempts { get; init; } = 5;
  public int LoginLockoutMinutes { get; init; } = 15;
  public bool PasswordExpirationEnabled { get; init; }
  public int PasswordDays { get; init; } = 90;
  public DateTimeOffset? PasswordEnabledAtUtc { get; init; }
  public bool InactivityEnabled { get; init; }
  public int InactivityDays { get; init; } = 90;
  public DateTimeOffset? InactivityEnabledAtUtc { get; init; }
  public int MinimumLength { get; init; } = 6;
  public int UniqueCharacters { get; init; } = 1;
  public bool RequireDigit { get; init; } = true;
  public bool RequireLowercase { get; init; } = true;
  public bool RequireUppercase { get; init; } = true;
  public bool RequireSymbol { get; init; } = true;

  public SaveAccountPolicyCommand ToCommand() => new() {
    Version = Version, MaxFailedLoginAttempts = MaxFailedLoginAttempts, LoginLockoutMinutes = LoginLockoutMinutes,
    PasswordExpirationEnabled = PasswordExpirationEnabled, PasswordDays = PasswordDays,
    InactivityEnabled = InactivityEnabled, InactivityDays = InactivityDays, MinimumLength = MinimumLength,
    UniqueCharacters = UniqueCharacters, RequireDigit = RequireDigit, RequireLowercase = RequireLowercase,
    RequireUppercase = RequireUppercase, RequireSymbol = RequireSymbol
  };
}
