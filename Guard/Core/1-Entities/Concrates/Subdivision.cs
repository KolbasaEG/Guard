namespace Guard.Core.Entities;

/// <summary>
/// Подразделение / отдел / орган организационной структуры системы Guard.
/// </summary>
public class Subdivision : BaseEntity
{
  /// <summary>
  /// Идентификатор подразделения для Path.
  /// </summary>
  public long? SubdivisionId { get; set; }

  /// <summary>
  /// Идентификатор подразделения АИС Личное дело.
  /// </summary>
  public long? ParentSubdivisionId { get; set; }

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
  /// Наименование, используемое при формировании должности.
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
  /// Признак: является ли подразделение отделом.
  /// </summary>
  public bool IsDepartment { get; set; }

  /// <summary>
  /// Штатная численность.
  /// </summary>
  public double StaffCount { get; set; }

  /// <summary>
  /// Порядковый номер внутри одного уровня иерархии.
  /// </summary>
  public int LevelOrder { get; set; }

  /// <summary>
  /// Путь подразделения (например: /1/4/12/).
  /// </summary>
  public string Path { get; set; } = string.Empty;

  // --- Составной внешний ключ на Classifiers (Тип и Код статуса) ---
  public int? StatusType { get; set; }
  public int? StatusCode { get; set; }
  public Classifier? StatusClassifier { get; set; }

  // --- Составной внешний ключ на OrganTypes (ID и Код органа) ---
  public int? OrganTypeId { get; set; }
  public int? OrganTypeCode { get; set; }
  public OrganType? OrganType { get; set; }

  public DateTime? UpdatedAt { get; set; }


  // --- Navigation ---
  public ICollection<Subdivision> Children { get; private set; } = new List<Subdivision>();
  public ICollection<Personal> Personals { get; private set; } = new List<Personal>();
  public ICollection<IpAddress> IpAddresses { get; private set; } = new List<IpAddress>();
}