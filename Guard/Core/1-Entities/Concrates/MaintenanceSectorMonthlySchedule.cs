using System.ComponentModel.DataAnnotations.Schema;

namespace Guard.Core.Entities;

[Table("MaintenanceSectorMonthlySchedules", Schema = "public")]
public class MaintenanceSectorMonthlySchedule : BaseEntity
{
  public Guid MaintenanceSectorId { get; set; }
  public MaintenanceSector MaintenanceSector { get; set; } = default!;

  /// <summary>
  /// Календарная дата
  /// </summary>
  public DateOnly Date { get; set; }

  /// <summary>
  /// Является ли этот день рабочим для участка
  /// </summary>
  public bool IsWorkDay { get; set; }

  /// <summary>
  /// Время начала смены
  /// </summary>
  public TimeSpan? WorkStart { get; set; }

  /// <summary>
  /// Время окончания смены
  /// </summary>
  public TimeSpan? WorkEnd { get; set; }

  /// <summary>
  /// Примечание (например, "Праздничный день", "Сокращенная смена")
  /// </summary>
  public string? Note { get; set; }
}