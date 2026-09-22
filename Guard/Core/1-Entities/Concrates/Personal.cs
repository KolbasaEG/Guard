namespace Guard.Core.Entities;

/// <summary>
/// Сущность персонала системы Guard.
/// </summary>
public class Personal : BaseEntity, IHasSubdivision
{
  /// <summary>
  /// Идентификатор подразделения (Внешний ключ).
  /// </summary>
  public Guid? SubdivisionId { get; set; }
  public Subdivision? Subdivision { get; set; }

  // --- Категория персонала ---
  public long? PersonnelCategoryTypeId { get; set; }
  public Classifier? PersonnelCategoryType { get; set; }

  public long? PersonnelCategoryCodeId { get; set; }
  public Classifier? PersonnelCategoryCode { get; set; }

  // --- Специальное звание ---
  public long? SpecialRankTypeId { get; set; }
  public Classifier? SpecialRankType { get; set; }

  public long? SpecialRankCodeId { get; set; }
  public Classifier? SpecialRankCode { get; set; }

  // --- Должность ---
  public long? PositionTypeId { get; set; }
  public Classifier? PositionType { get; set; }

  public long? PositionCodeId { get; set; }
  public Classifier? PositionCode { get; set; }

  // --- Категория рабочего/служащего ---
  public long? WorkerCategoryTypeId { get; set; }
  public Classifier? WorkerCategoryType { get; set; }

  public long? WorkerCategoryCodeId { get; set; }
  public Classifier? WorkerCategoryCode { get; set; }

  // --- Персональные данные ---
  /// <summary>
  /// Фамилия.
  /// </summary>
  public string LastName { get; set; } = default!;

  /// <summary>
  /// Имя.
  /// </summary>
  public string FirstName { get; set; } = default!;

  /// <summary>
  /// Отчество.
  /// </summary>
  public string? MiddleName { get; set; }

  /// <summary>
  /// Фамилия и инициалы.
  /// </summary>
  public string? FullName { get; set; }

  /// <summary>
  /// Год принятия на службу.
  /// </summary>
  public int? EnlistmentYear { get; set; }

  /// <summary>
  /// Личный номер.
  /// </summary>
  public string? PersonalNumber { get; set; }

  // --- ФИО в родительном падеже ---
  /// <summary>
  /// Фамилия в родительном падеже.
  /// </summary>
  public string? LastNameGen { get; set; }

  /// <summary>
  /// Имя в родительном падеже.
  /// </summary>
  public string? FirstNameGen { get; set; }

  /// <summary>
  /// Отчество в родительном падеже.
  /// </summary>
  public string? MiddleNameGen { get; set; }

  // --- Статусы записи ---
  public long? StatusTypeId { get; set; }
  public Classifier? StatusType { get; set; }

  public long? StatusCodeId { get; set; }
  public Classifier? StatusCode { get; set; }

  /// <summary>
  /// Дата последнего обновления информации.
  /// </summary>
  public DateTime? UpdatedAt { get; set; }



  /// <summary>
  /// Связанная учетная запись пользователя.
  /// </summary>
  public ApplicationUser? User { get; set; }
}