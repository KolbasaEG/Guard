using System.Net;

namespace Guard.Core.Entities;

/// <summary>
/// Справочник IP-адресов и подсетей системы Guard.
/// 
/// Назначение:
/// - Центральный каталог всех известных IP-адресов и CIDR-подсетей.
/// - Может использоваться для пользователей, подразделений, активов и других сущностей.
/// - Поддерживает как точные IP, так и диапазоны (CIDR).
/// </summary>
public class IpAddress : BaseEntity
{
  /// <summary>
  /// IP-адрес или CIDR-нотация.
  /// Примеры: "192.168.1.50", "10.0.0.0/24", "2001:db8::/32"
  /// </summary>
  public string Address { get; set; } = default!;

  /// <summary>
  /// Человекочитаемое название IP-адреса или подсети.
  /// Пример: "Офис Москва - Основная сеть"
  /// </summary>
  public string Name { get; set; } = default!;

  /// <summary>
  /// Дополнительное описание.
  /// </summary>
  public string? Description { get; set; }

  /// <summary>
  /// Пользователи, которым назначен данный IP (прямая навигация).
  /// </summary>
  public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();

  /// <summary>
  /// Назначения IP пользователям с метаданными (join-сущность).
  /// </summary>
  public ICollection<UserIpAddress> UserAssignments { get; set; } = new List<UserIpAddress>();

}
