using System.ComponentModel.DataAnnotations.Schema;

namespace Guard.Core.Entities;

[Table("MaintenanceSectorWeeklyPatterns", Schema = "public")]
public class MaintenanceSectorWeeklyPattern : BaseEntity
{
  public Guid MaintenanceSectorId { get; set; }
  public MaintenanceSector MaintenanceSector { get; set; } = default!;

  /// <summary>
  /// День недели (0 = Sunday, 1 = Monday ...)
  /// </summary>
  public DayOfWeek DayOfWeek { get; set; }

  /// <summary>
  /// Признак рабочего дня
  /// </summary>
  public bool IsWorkDay { get; set; }

  /// <summary>
  /// Время начала смены (null, если выходной)
  /// </summary>
  public TimeSpan? WorkStart { get; set; }

  /// <summary>
  /// Время окончания смены (null, если выходной)
  /// </summary>
  public TimeSpan? WorkEnd { get; set; }
}