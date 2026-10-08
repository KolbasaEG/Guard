namespace Guard.Core.Services.DTOs;
public sealed record PersonalDetailsDto
{
  public Guid Id { get; init; }
  public Guard.Core.Enums.Status Status { get; init; }
  public DateTime InsertedDate { get; init; }
  public DateTime? LastModifiedDate { get; init; }
  public string CreatedBy { get; init; } = "";
  public string? ModifiedBy { get; init; }
  public long PersonalId { get; init; }
  public Guid? SubdivisionId { get; init; }
  public long PersonalSubdivisionId { get; init; }
  public int? PersonnelCategoryType { get; init; }
  public int? PersonnelCategoryCode { get; init; }
  public int? SpecialRankType { get; init; }
  public int? SpecialRankCode { get; init; }
  public int? PositionType { get; init; }
  public int? PositionCode { get; init; }
  public int? WorkerCategoryType { get; init; }
  public int? WorkerCategoryCode { get; init; }
  public string LastName { get; init; } = "";
  public string FirstName { get; init; } = "";
  public string? MiddleName { get; init; }
  public string? FullName { get; init; }
  public int? EnlistmentYear { get; init; }
  public string? PersonalNumber { get; init; }
  public string? LastNameGen { get; init; }
  public string? FirstNameGen { get; init; }
  public string? MiddleNameGen { get; init; }
  public int? StatusType { get; init; }
  public int? StatusCode { get; init; }
  public DateTime? UpdatedAt { get; init; }
  public string? SubdivisionName { get; init; }
  public string? PersonnelCategoryName { get; init; }
  public string? SpecialRankName { get; init; }
  public string? PositionName { get; init; }
  public string? WorkerCategoryName { get; init; }
  public string? StatusClassifierName { get; init; }
  public string? UserId { get; init; }
  public string? UserName { get; init; }
  public string[] IpAddresses { get; init; } = [];
}
