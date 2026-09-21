// Guard.Core/1-Entities/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;

namespace Guard.Core.Entities;

public class ApplicationUser : IdentityUser
{

  /// <summary>
  /// Прямая навигация к назначенным IP-адресам.
  /// </summary>
  public ICollection<IpAddress> IpAddresses { get; private set; } = new List<IpAddress>();

  /// <summary>
  /// Назначения IP с метаданными (когда, кем, с какой целью).
  /// </summary>
  public ICollection<UserIpAddress> UserIpAddresses { get; private set; } = new List<UserIpAddress>();
}