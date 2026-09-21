using Guard.Core.Enums;

namespace Guard.Core.Entities;

/// <summary>
/// «Универсальный справочник» (Universal Lookup / Single Classifier Table)
/// 
/// Назначение:
/// в одной таблице хранятся все типы классификаторов
/// системы (например: звания, должности, категории сотрудников(работников)).
/// </summary>

public class Classifier
{
  public long Id { get; set; }
  // Тип справочника
  public ClassifierType Type { get; set; }

  // Код значения внутри справочника (1, 2, 3...)
  public int Code { get; set; }

  // Наименование справочника (например, "Тип документа")
  public string ClassifierName { get; set; } = string.Empty;

  // Отображаемое значение (например, "Паспорт", "Водительское удостоверение")
  public string Value { get; set; } = string.Empty;

  // Актуальность (0 - неактивен, 1 - активен)
  public bool IsActive { get; set; } = true;

  public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
