using Guard.Core.Services.DTOs;

namespace Guard.Components.Pages.AccountPolicy;

public sealed class AccountPolicyForm
{
  public int MaxFailedLoginAttempts { get; set; } = 5;
  public int LoginLockoutMinutes { get; set; } = 15;
  public bool PasswordExpirationEnabled { get; set; }
  public int PasswordDays { get; set; } = 90;
  public bool InactivityEnabled { get; set; }
  public int InactivityDays { get; set; } = 90;
  public int MinimumLength { get; set; } = 6;
  public int UniqueCharacters { get; set; } = 1;
  public bool RequireDigit { get; set; } = true;
  public bool RequireLowercase { get; set; } = true;
  public bool RequireUppercase { get; set; } = true;
  public bool RequireSymbol { get; set; } = true;

  public static AccountPolicyForm From(AccountPolicySettingsDto value) => new() {
    MaxFailedLoginAttempts = value.MaxFailedLoginAttempts,
    LoginLockoutMinutes = value.LoginLockoutMinutes,
    PasswordExpirationEnabled = value.PasswordExpirationEnabled,
    PasswordDays = value.PasswordDays,
    InactivityEnabled = value.InactivityEnabled,
    InactivityDays = value.InactivityDays,
    MinimumLength = value.MinimumLength,
    UniqueCharacters = value.UniqueCharacters,
    RequireDigit = value.RequireDigit,
    RequireLowercase = value.RequireLowercase,
    RequireUppercase = value.RequireUppercase,
    RequireSymbol = value.RequireSymbol
  };

  public SaveAccountPolicyCommand ToCommand(Guid version) => new() {
    Version = version,
    MaxFailedLoginAttempts = MaxFailedLoginAttempts,
    LoginLockoutMinutes = LoginLockoutMinutes,
    PasswordExpirationEnabled = PasswordExpirationEnabled,
    PasswordDays = PasswordDays,
    InactivityEnabled = InactivityEnabled,
    InactivityDays = InactivityDays,
    MinimumLength = MinimumLength,
    UniqueCharacters = UniqueCharacters,
    RequireDigit = RequireDigit,
    RequireLowercase = RequireLowercase,
    RequireUppercase = RequireUppercase,
    RequireSymbol = RequireSymbol
  };
}
