using Guard.Core.Enums;

namespace Guard.Core.Services.DTOs;

public sealed record PersonalEditorDto
{
  public Guid Id { get; init; }
  public Guid Version { get; set; }
  public Status Status { get; set; } = Status.Inserted;
  public Guid? SubdivisionId { get; set; }
  public string LastName { get; set; } = "";
  public string FirstName { get; set; } = "";
  public string? MiddleName { get; set; }
  public string? FullName { get; set; }
  public int? EnlistmentYear { get; set; }
  public string? PersonalNumber { get; set; }
  public string? LastNameGen { get; set; }
  public string? FirstNameGen { get; set; }
  public string? MiddleNameGen { get; set; }
  public int? PersonnelCategoryType { get; set; }
  public int? PersonnelCategoryCode { get; set; }
  public int? SpecialRankType { get; set; }
  public int? SpecialRankCode { get; set; }
  public int? PositionType { get; set; }
  public int? PositionCode { get; set; }
  public int? WorkerCategoryType { get; set; }
  public int? WorkerCategoryCode { get; set; }
  public int? StatusType { get; set; }
  public int? StatusCode { get; set; }

  public PersonalFieldsDto ToFields() => new() {
    SubdivisionId = SubdivisionId,
    LastName = LastName,
    FirstName = FirstName,
    MiddleName = MiddleName,
    FullName = FullName,
    EnlistmentYear = EnlistmentYear,
    PersonalNumber = PersonalNumber,
    LastNameGen = LastNameGen,
    FirstNameGen = FirstNameGen,
    MiddleNameGen = MiddleNameGen,
    PersonnelCategoryType = PersonnelCategoryType,
    PersonnelCategoryCode = PersonnelCategoryCode,
    SpecialRankType = SpecialRankType,
    SpecialRankCode = SpecialRankCode,
    PositionType = PositionType,
    PositionCode = PositionCode,
    WorkerCategoryType = WorkerCategoryType,
    WorkerCategoryCode = WorkerCategoryCode,
    StatusType = StatusType,
    StatusCode = StatusCode
  };
}

public sealed record PersonalSubdivisionOptionDto(Guid Id, string Name);
