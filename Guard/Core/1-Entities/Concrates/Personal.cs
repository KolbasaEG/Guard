namespace Guard.Core.Entities;

/// <summary>
/// ���������� ��������� ������� Guard.
/// </summary>
public class Personal : BaseEntity, IHasSubdivision
{
  public Guid Version { get; set; } = Guid.NewGuid();

  /// <summary>
  /// ������������� ���������� ��� ������ ����.
  /// </summary>
  public long PersonalId { get; set; }
  /// <summary>
  /// ������������� ������������� (������� ����).
  /// </summary>
  public Guid? SubdivisionId { get; set; }
  public Subdivision? Subdivision { get; set; }

  /// <summary>
  /// ������������� ������������� ��� ������ ����.
  /// </summary>
  public long PersonalSubdivisionId { get; set; }

  // --- ��������� ��������� ---
  public int? PersonnelCategoryType { get; set; }
  public int? PersonnelCategoryCode { get; set; }
  public Classifier? PersonnelCategory { get; set; }

  // --- ����������� ������ ---
  public int? SpecialRankType { get; set; }
  public int? SpecialRankCode { get; set; }
  public Classifier? SpecialRank { get; set; }

  // --- ��������� ---
  public int? PositionType { get; set; }
  public int? PositionCode { get; set; }
  public Classifier? Position { get; set; }

  // --- ��������� ��������/��������� ---
  public int? WorkerCategoryType { get; set; }
  public int? WorkerCategoryCode { get; set; }
  public Classifier? WorkerCategory { get; set; }

  // --- ������������ ������ ---
  public string LastName { get; set; } = default!;
  public string FirstName { get; set; } = default!;
  public string? MiddleName { get; set; }
  public string? FullName { get; set; }

  public int? EnlistmentYear { get; set; }
  public string? PersonalNumber { get; set; }

  // --- ��� � ����������� ������ ---
  public string? LastNameGen { get; set; }
  public string? FirstNameGen { get; set; }
  public string? MiddleNameGen { get; set; }

  // --- ��������� ������� ������ ������������ ---
  public ApplicationUser? User { get; set; }

  // --- ������������� ������� ---
  public int? StatusType { get; set; }
  public int? StatusCode { get; set; }
  public Classifier? StatusClassifier { get; set; }

  public DateTime? UpdatedAt { get; set; }

  // ������ ��������� ����������� IP-������� ����������
  public virtual ICollection<IpAddress> IpAddresses { get; set; } = new List<IpAddress>();
}
