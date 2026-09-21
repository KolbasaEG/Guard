namespace Guard.Core.Entities;

/// <summary>
/// Подразделение / отдел / орган организационной структуры системы Guard.
/// 
/// Назначение:
/// - Иерархический справочник организационной структуры (adjacency list).
/// - Поддерживает вложенность любой глубины через ParentId.
/// </summary>
/// 
public class Subdivision : BaseEntity
{
  /// <summary>
  /// Идентификатор подразделения для Path.
  /// null = корневой узел.
  /// </summary>
  public long SubdivisionId { get; set; }

  /// <summary>
  /// Идентификатор родительского подразделения.
  /// null = корневой узел.
  /// </summary>
  public Guid? ParentId { get; set; }
  public Subdivision? Parent { get; private set; }

  /// <summary>
  /// Полное наименование подразделения.
  /// </summary>
  public string Name { get; set; } = default!;

  /// <summary>
  /// Наименование, используемое при формировании должности
  /// (например: "Управление информационной безопасности").
  /// </summary>
  public string? PositionFormationName { get; set; }

  /// <summary>
  /// Почтовый индекс.
  /// </summary>
  public string? PostalCode { get; set; }

  /// <summary>
  /// Город / полный адрес.
  /// </summary>
  public string? Address { get; set; }

  /// <summary>
  /// Телефон.
  /// </summary>
  public string? Phone { get; set; }

  /// <summary>
  /// Факс.
  /// </summary>
  public string? Fax { get; set; }

  /// <summary>
  /// Признак: является ли подразделение отделом
  /// </summary>
  public bool IsDepartment { get; set; }

  /// <summary>
  /// Штатная численность 
  /// </summary>
  public int StaffCount { get; set; }

  /// <summary>
  /// Порядковый номер внутри одного уровня иерархии.
  /// </summary>
  public int LevelOrder { get; set; }

  /// <summary>
  /// Путь подразделения. Хранится как обычная строка с разделителями, например: /1/4/12/
  /// </summary>
  public string Path { get; set; } = string.Empty;

  public long? StatusTypeId { get; set; }
  public Classifier? StatusType { get; set; }
  public long? StatusCodeId { get; set; }
  public Classifier? StatusCode { get; set; }
  public long? OrganTypeCodeId { get; set; }
  public Classifier? OrganTypeCode { get; set; }
  public long? OrganTypeId { get; set; }
  public Classifier? OrganType { get; set; }


  // --- Navigation ---
  
  public ICollection<Subdivision> Childrens { get; private set; } = new List<Subdivision>();
}
