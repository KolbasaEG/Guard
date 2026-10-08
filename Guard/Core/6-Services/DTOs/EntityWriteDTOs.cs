using Guard.Core.Entities;
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
  public static PersonalFieldsDto From(Personal entity) => new() {
    SubdivisionId = entity.SubdivisionId,
    LastName = entity.LastName,
    FirstName = entity.FirstName,
    MiddleName = entity.MiddleName,
    FullName = entity.FullName,
    EnlistmentYear = entity.EnlistmentYear,
    PersonalNumber = entity.PersonalNumber,
    LastNameGen = entity.LastNameGen,
    FirstNameGen = entity.FirstNameGen,
    MiddleNameGen = entity.MiddleNameGen,
    PersonnelCategoryType = entity.PersonnelCategoryType,
    PersonnelCategoryCode = entity.PersonnelCategoryCode,
    SpecialRankType = entity.SpecialRankType,
    SpecialRankCode = entity.SpecialRankCode,
    PositionType = entity.PositionType,
    PositionCode = entity.PositionCode,
    WorkerCategoryType = entity.WorkerCategoryType,
    WorkerCategoryCode = entity.WorkerCategoryCode,
    StatusType = entity.StatusType,
    StatusCode = entity.StatusCode
  };
  public void ApplyTo(Personal entity) {
    entity.SubdivisionId = SubdivisionId;
    entity.LastName = LastName;
    entity.FirstName = FirstName;
    entity.MiddleName = MiddleName;
    entity.FullName = FullName;
    entity.EnlistmentYear = EnlistmentYear;
    entity.PersonalNumber = PersonalNumber;
    entity.LastNameGen = LastNameGen;
    entity.FirstNameGen = FirstNameGen;
    entity.MiddleNameGen = MiddleNameGen;
    entity.PersonnelCategoryType = PersonnelCategoryType;
    entity.PersonnelCategoryCode = PersonnelCategoryCode;
    entity.SpecialRankType = SpecialRankType;
    entity.SpecialRankCode = SpecialRankCode;
    entity.PositionType = PositionType;
    entity.PositionCode = PositionCode;
    entity.WorkerCategoryType = WorkerCategoryType;
    entity.WorkerCategoryCode = WorkerCategoryCode;
    entity.StatusType = StatusType;
    entity.StatusCode = StatusCode;
  }
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
  public static SubdivisionFieldsDto From(Subdivision entity) => new() {
    Name = entity.Name,
    PositionFormationName = entity.PositionFormationName,
    PostalCode = entity.PostalCode,
    Address = entity.Address,
    Phone = entity.Phone,
    Fax = entity.Fax,
    IsDepartment = entity.IsDepartment,
    StaffCount = entity.StaffCount,
    LevelOrder = entity.LevelOrder,
    StatusType = entity.StatusType,
    StatusCode = entity.StatusCode,
    OrganTypeId = entity.OrganTypeId,
    OrganTypeCode = entity.OrganTypeCode
  };
  public void ApplyTo(Subdivision entity) {
    entity.Name = Name;
    entity.PositionFormationName = PositionFormationName;
    entity.PostalCode = PostalCode;
    entity.Address = Address;
    entity.Phone = Phone;
    entity.Fax = Fax;
    entity.IsDepartment = IsDepartment;
    entity.StaffCount = StaffCount;
    entity.LevelOrder = LevelOrder;
    entity.StatusType = StatusType;
    entity.StatusCode = StatusCode;
    entity.OrganTypeId = OrganTypeId;
    entity.OrganTypeCode = OrganTypeCode;
  }
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
  public static ClassifierFieldsDto From(Classifier entity) => new() {
    Type = entity.Type,
    Code = entity.Code,
    ClassifierName = entity.ClassifierName,
    Value = entity.Value,
    IsActive = entity.IsActive
  };
  public void ApplyTo(Classifier entity) {
    entity.Type = Type;
    entity.Code = Code;
    entity.ClassifierName = ClassifierName;
    entity.Value = Value;
    entity.IsActive = IsActive;
  }
}
public record CreateClassifierDto(ClassifierFieldsDto Fields);
public record EditClassifierDto(int Id, Guid Version, ClassifierFieldsDto Fields);
public record OrganTypeFieldsDto
{
  public int ClassifierType { get; init; }
  public int Code { get; init; }
  public string Name { get; init; } = "";
  public static OrganTypeFieldsDto From(OrganType entity) => new() {
    ClassifierType = entity.ClassifierType,
    Code = entity.Code,
    Name = entity.Name
  };
  public void ApplyTo(OrganType entity) {
    entity.ClassifierType = ClassifierType;
    entity.Code = Code;
    entity.Name = Name;
  }
}
public record CreateOrganTypeDto(OrganTypeFieldsDto Fields);
public record EditOrganTypeDto(int Id, Guid Version, OrganTypeFieldsDto Fields);
