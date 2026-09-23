namespace Guard.Core.Entities;

/// <summary>
/// Справочник IP-адресов и подсетей системы Guard.
/// 
/// Назначение:
/// - Центральный каталог всех известных IP-адресов и CIDR-подсетей.
/// - Может использоваться для пользователей, подразделений, активов и других сущностей.
/// - Поддерживает как точные IP, так и диапазоны (CIDR).
/// </summary>
public class IpAddress : BaseEntity, IHasSubdivision
{
  /// <summary>
  /// IP-адрес 
  /// </summary>
  public string Address { get; set; } = default!;

  /// <summary>
  /// Описание.
  /// </summary>
  public string? Description { get; set; }

  /// <summary>
  /// Идентификатор подразделения (внешний ключ).
  /// </summary>
  public Guid? SubdivisionId { get; set; }
  public Subdivision? Subdivision { get; set; }

  /// <summary>
  /// Пользователи, которым назначен данный IP (прямая навигация).
  /// </summary>
  public virtual ICollection<Personal> Personals { get; set; } = new List<Personal>();
}
