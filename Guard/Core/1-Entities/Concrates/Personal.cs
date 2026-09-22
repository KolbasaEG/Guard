namespace Guard.Core.Entities;

/// <summary>
/// Справочник персонала системы Guard.
/// </summary>
public class Personal : BaseEntity, IHasSubdivision
{

  /// <summary>
  /// Идентификатор сотрудника АИС Личное дело.
  /// </summary>
  public long PersonalId { get; set; }
  /// <summary>
  /// Идентификатор подразделения (внешний ключ).
  /// </summary>
  public Guid? SubdivisionId { get; set; }
  public Subdivision? Subdivision { get; set; }

  /// <summary>
  /// Идентификатор подразделения АИС Личное дело.
  /// </summary>
  public long PersonalSubdivisionId { get; set; }

  // --- Категория персонала ---
  public int? PersonnelCategoryType { get; set; }
  public int? PersonnelCategoryCode { get; set; }
  public Classifier? PersonnelCategory { get; set; }

  // --- Специальное звание ---
  public int? SpecialRankType { get; set; }
  public int? SpecialRankCode { get; set; }
  public Classifier? SpecialRank { get; set; }

  // --- Должность ---
  public int? PositionType { get; set; }
  public int? PositionCode { get; set; }
  public Classifier? Position { get; set; }

  // --- Категория рабочего/служащего ---
  public int? WorkerCategoryType { get; set; }
  public int? WorkerCategoryCode { get; set; }
  public Classifier? WorkerCategory { get; set; }

  // --- Персональные данные ---
  public string LastName { get; set; } = default!;
  public string FirstName { get; set; } = default!;
  public string? MiddleName { get; set; }
  public string? FullName { get; set; }

  public int? EnlistmentYear { get; set; }
  public string? PersonalNumber { get; set; }

  // --- ФИО в родительном падеже ---
  public string? LastNameGen { get; set; }
  public string? FirstNameGen { get; set; }
  public string? MiddleNameGen { get; set; }

  // --- Связанная учетная запись пользователя ---
  public ApplicationUser? User { get; set; }

  // --- Классификатор статуса ---
  public int? StatusType { get; set; }
  public int? StatusCode { get; set; }
  public Classifier? StatusClassifier { get; set; }

  public DateTime? UpdatedAt { get; set; }
}