namespace Guard.Core.Entities;

public class AccountPolicy
{
  public int Id { get; set; } = 1;
  public Guid Version { get; set; } = Guid.NewGuid();
  public bool PasswordExpirationEnabled { get; set; }
  public int PasswordDays { get; set; } = 90;
  public DateTimeOffset? PasswordEnabledAtUtc { get; set; }
  public bool InactivityEnabled { get; set; }
  public int InactivityDays { get; set; } = 90;
  public DateTimeOffset? InactivityEnabledAtUtc { get; set; }
  public int MinimumLength { get; set; } = 6;
  public int UniqueCharacters { get; set; } = 1;
  public bool RequireDigit { get; set; } = true;
  public bool RequireLowercase { get; set; } = true;
  public bool RequireUppercase { get; set; } = true;
  public bool RequireSymbol { get; set; } = true;
}
