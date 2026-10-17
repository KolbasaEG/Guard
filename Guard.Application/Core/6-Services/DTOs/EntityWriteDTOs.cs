namespace Guard.Core.Services.DTOs;
public record PersonalFieldsDto
{
  public Guid? SubdivisionId { get; init; }
  public string LastName { get; init; } = "";
  public string FirstName { get; init; } = "";
  public string? MiddleName { get; init; }
  public string? FullName { get; init; }
  public int? EnlistmentYear { get; init; }
  public string? PersonalNumber { get; init; }
  public string? LastNameGen { get; init; }
  public string? FirstNameGen { get; init; }
  public string? MiddleNameGen { get; init; }
  public int? PersonnelCategoryType { get; init; }
  public int? PersonnelCategoryCode { get; init; }
  public int? SpecialRankType { get; init; }
  public int? SpecialRankCode { get; init; }
  public int? PositionType { get; init; }
  public int? PositionCode { get; init; }
  public int? WorkerCategoryType { get; init; }
  public int? WorkerCategoryCode { get; init; }
  public int? StatusType { get; init; }
  public int? StatusCode { get; init; }
}
public record CreatePersonalDto(PersonalFieldsDto Fields);
public record EditPersonalDto(Guid Id, Guid Version, PersonalFieldsDto Fields);
public record SubdivisionFieldsDto
{
  public string Name { get; init; } = "";
  public string? PositionFormationName { get; init; }
  public string? PostalCode { get; init; }
  public string? Address { get; init; }
  public string? Phone { get; init; }
  public string? Fax { get; init; }
  public bool IsDepartment { get; init; }
  public double StaffCount { get; init; }
  public int LevelOrder { get; init; }
  public int? StatusType { get; init; }
  public int? StatusCode { get; init; }
  public int? OrganTypeId { get; init; }
  public int? OrganTypeCode { get; init; }
}
public record CreateSubdivisionDto(SubdivisionFieldsDto Fields, Guid? ParentId = null);
public record EditSubdivisionDto(Guid Id, Guid Version, SubdivisionFieldsDto Fields);
public record ClassifierFieldsDto
{
  public int Type { get; init; }
  public int Code { get; init; }
  public string ClassifierName { get; init; } = "";
  public string Value { get; init; } = "";
  public bool IsActive { get; init; }
}
public record CreateClassifierDto(ClassifierFieldsDto Fields);
public record EditClassifierDto(int Id, Guid Version, ClassifierFieldsDto Fields);
