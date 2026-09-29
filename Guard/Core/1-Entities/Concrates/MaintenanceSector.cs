using System.ComponentModel.DataAnnotations.Schema;

namespace Guard.Core.Entities;

[Table("MaintenanceSectors", Schema = "public")]
public class MaintenanceSector : BaseEntity, IHasSubdivision
{
  public string Name { get; set; } = string.Empty;
  public string? Description { get; set; }

  // Привязка к подразделению (1 подразделение)
  public Guid? SubdivisionId { get; set; }
  public Subdivision Subdivision { get; set; } = default!;

  // Ответственный сотрудник за участок
  public Guid ResponsiblePersonalId { get; set; }
  public Personal ResponsiblePersonal { get; set; } = default!;

  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  // ================= Навигационные свойства =================

  // Связь с объектами участка (многие-ко-многим)
  public ICollection<MaintenanceSectorObject> SectorObjects { get; set; } = new List<MaintenanceSectorObject>();

  // 1. Шаблон рабочей недели участка (до 7 записей)
  public ICollection<MaintenanceSectorWeeklyPattern> WeeklyPatterns { get; set; } = new List<MaintenanceSectorWeeklyPattern>();

  // 2. Сгенерированный и скорректированный график работы участка на месяц
  public ICollection<MaintenanceSectorMonthlySchedule> MonthlySchedules { get; set; } = new List<MaintenanceSectorMonthlySchedule>();

  // 3. Сетка отметок регламентов по объектам ("крестики")
  public ICollection<MaintenanceSectorObjectSchedule> ObjectSchedules { get; set; } = new List<MaintenanceSectorObjectSchedule>();
}