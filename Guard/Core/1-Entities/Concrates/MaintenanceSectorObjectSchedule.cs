using System.ComponentModel.DataAnnotations.Schema;

namespace Guard.Core.Entities;

[Table("MaintenanceSectorObjectSchedules", Schema = "public")]
public class MaintenanceSectorObjectSchedule : BaseEntity
{
  public Guid MaintenanceSectorId { get; set; }
  public MaintenanceSector MaintenanceSector { get; set; } = default!;

  public Guid ProtectedObjectId { get; set; }
  public ProtectedObject ProtectedObject { get; set; } = default!;

  /// <summary>
  /// Дата обслуживания
  /// </summary>
  public DateOnly Date { get; set; }

  /// <summary>
  /// Флаг «Крестик» — запланирован регламент на этот день
  /// </summary>
  public bool IsScheduled { get; set; }

  /// <summary>
  /// Флаг фактического выполнения
  /// </summary>
  public bool IsCompleted { get; set; }

  /// <summary>
  /// Вид регламента или примечание ("ТО-1", "Проверка датчиков")
  /// </summary>
  public string? Note { get; set; }
}