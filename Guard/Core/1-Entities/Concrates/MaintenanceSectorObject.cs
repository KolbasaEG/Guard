using System.ComponentModel.DataAnnotations.Schema;

namespace Guard.Core.Entities;

[Table("MaintenanceSectorObjects", Schema = "public")]
public class MaintenanceSectorObject : BaseEntity
{
  public Guid MaintenanceSectorId { get; set; }
  public MaintenanceSector MaintenanceSector { get; set; } = default!;

  public Guid ProtectedObjectId { get; set; }
  public ProtectedObject ProtectedObject { get; set; } = default!;

  public DateTime AttachedAt { get; set; } = DateTime.UtcNow;
}