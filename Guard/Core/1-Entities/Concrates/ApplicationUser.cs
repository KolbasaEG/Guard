// Guard.Core/1-Entities/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;

namespace Guard.Core.Entities;

public class ApplicationUser : IdentityUser
{
  public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
  public DateTimeOffset? PasswordChangedAtUtc { get; set; }
  public DateTimeOffset? LastActivityAtUtc { get; set; }
  public DateTimeOffset? UnblockedAtUtc { get; set; }
  public bool MustChangePassword { get; set; }
  public string? AccountBlockReason { get; set; }
  /// <summary>
  /// Идентификатор пользователя.
  /// </summary>
  public Guid? PersonalId { get; set; }
  public Personal? Personal { get; set; }
}
