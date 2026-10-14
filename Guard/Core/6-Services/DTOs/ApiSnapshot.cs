using Guard.Core.Entities;

namespace Guard.Core.Services.DTOs;

public sealed class ApiSubdivision
{
  public long Id { get; set; }
  public long? ParentId { get; set; }
  public string Name { get; set; } = "";
  public string? PositionFormationName { get; set; }
  public string? PostalCode { get; set; }
  public string? Address { get; set; }
  public string? Phone { get; set; }
  public string? Fax { get; set; }
  public bool IsDepartment { get; set; }
  public double StaffCount { get; set; }
  public int LevelOrder { get; set; }
  public int? StatusType { get; set; }
  public int? StatusCode { get; set; }
  public int? OrganTypeId { get; set; }
  public int? OrganTypeCode { get; set; }
  public DateTimeOffset UpdatedAt { get; set; }

  public SubdivisionFieldsDto Fields => new() {
    Name = Name, PositionFormationName = PositionFormationName, PostalCode = PostalCode,
    Address = Address, Phone = Phone, Fax = Fax, IsDepartment = IsDepartment,
    StaffCount = StaffCount, LevelOrder = LevelOrder, StatusType = StatusType,
    StatusCode = StatusCode, OrganTypeId = OrganTypeId, OrganTypeCode = OrganTypeCode
  };
}

public sealed class ApiPersonal
{
  public long Id { get; set; }
  public long SubdivisionId { get; set; }
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
  public DateTimeOffset UpdatedAt { get; set; }

  public PersonalFieldsDto Fields(Guid? subdivisionId) => new() {
    SubdivisionId = subdivisionId, LastName = LastName, FirstName = FirstName,
    MiddleName = MiddleName, FullName = FullName, EnlistmentYear = EnlistmentYear,
    PersonalNumber = PersonalNumber, LastNameGen = LastNameGen, FirstNameGen = FirstNameGen,
    MiddleNameGen = MiddleNameGen, PersonnelCategoryType = PersonnelCategoryType,
    PersonnelCategoryCode = PersonnelCategoryCode, SpecialRankType = SpecialRankType,
    SpecialRankCode = SpecialRankCode, PositionType = PositionType, PositionCode = PositionCode,
    WorkerCategoryType = WorkerCategoryType, WorkerCategoryCode = WorkerCategoryCode,
    StatusType = StatusType, StatusCode = StatusCode
  };
}

public record ApiSnapshot(IReadOnlyList<RemoteClassifierDto> Classifiers,
  IReadOnlyList<ApiSubdivision> Subdivisions, IReadOnlyList<ApiPersonal> Personnel)
{
  public DateOnly? UpdatedSince { get; init; }
  public long? HeadSubdivisionId { get; init; }
  public IReadOnlyList<ApiPersonnelExitEvent> ExitEvents { get; init; } = [];
}

public sealed class ApiPersonnelExitEvent
{
  public long PersonalId { get; set; }
  public DateTimeOffset EventAt { get; set; }
}

public record SynchronizationOverview(SynchronizationSettings Settings, Guid? PoolId,
  DateTime? ActualAt, SynchronizationRun? LastAttempt, bool CanRun);

public record DirectoryActuality(DataManagementMode Mode, DateTime? ActualAt);
