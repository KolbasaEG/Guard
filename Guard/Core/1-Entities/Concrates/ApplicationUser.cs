// Guard.Core/1-Entities/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;

namespace Guard.Core.Entities;

public class ApplicationUser : IdentityUser
{
  /// <summary>
  /// Идентификатор пользователя.
  /// </summary>
  public Guid? PersonalId { get; set; }
  public Personal? Personal { get; set; }
}