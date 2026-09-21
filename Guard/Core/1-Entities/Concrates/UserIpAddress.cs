namespace Guard.Core.Entities;

/// <summary>
/// Join-сущность для связи многие-ко-многим между ApplicationUser и IpAddress.
/// 
/// Хранит метаданные назначения:
/// - Кто назначил IP
/// - Когда назначил
/// - С какой целью (Purpose)
/// - Активно ли назначение
/// </summary>
public class UserIpAddress
{
  public Guid Id { get; private set; } = Guid.NewGuid();

  public string UserId { get; private set; } = default!;
  public Guid IpAddressId { get; private set; }

  public DateTime AssignedAt { get; private set; } = DateTime.UtcNow;
  public string AssignedBy { get; private set; } = default!;

  /// <summary>
  /// Цель назначения IP-адреса.
  /// Примеры: "Login", "Monitoring", "VPN", "Office"
  /// </summary>
  public string? Purpose { get; private set; }

  public bool IsActive { get; private set; } = true;
  public string? Description { get; private set; }

  public ApplicationUser User { get; private set; } = default!;
  public IpAddress IpAddress { get; private set; } = default!;

  private UserIpAddress() { }

  public UserIpAddress(string userId, Guid ipAddressId, string assignedBy, string? purpose = "Login", string? description = null)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(userId);
    ArgumentException.ThrowIfNullOrWhiteSpace(assignedBy);

    UserId = userId;
    IpAddressId = ipAddressId;
    AssignedBy = assignedBy;
    Purpose = purpose;
    Description = description;
  }

  public void Deactivate() => IsActive = false;
}
