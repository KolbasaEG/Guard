using Guard.Core.Entities;
using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

// Server mapping copies editable fields only; identity, audit and lifecycle stay with the operation.
internal static class EntityWriteMapping
{
  internal static PersonalFieldsDto From(Personal entity) => new() {
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

  internal static void ApplyTo(this PersonalFieldsDto fields, Personal entity) {
    entity.SubdivisionId = fields.SubdivisionId;
    entity.LastName = fields.LastName;
    entity.FirstName = fields.FirstName;
    entity.MiddleName = fields.MiddleName;
    entity.FullName = fields.FullName;
    entity.EnlistmentYear = fields.EnlistmentYear;
    entity.PersonalNumber = fields.PersonalNumber;
    entity.LastNameGen = fields.LastNameGen;
    entity.FirstNameGen = fields.FirstNameGen;
    entity.MiddleNameGen = fields.MiddleNameGen;
    entity.PersonnelCategoryType = fields.PersonnelCategoryType;
    entity.PersonnelCategoryCode = fields.PersonnelCategoryCode;
    entity.SpecialRankType = fields.SpecialRankType;
    entity.SpecialRankCode = fields.SpecialRankCode;
    entity.PositionType = fields.PositionType;
    entity.PositionCode = fields.PositionCode;
    entity.WorkerCategoryType = fields.WorkerCategoryType;
    entity.WorkerCategoryCode = fields.WorkerCategoryCode;
    entity.StatusType = fields.StatusType;
    entity.StatusCode = fields.StatusCode;
  }

  internal static SubdivisionFieldsDto From(Subdivision entity) => new() {
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

  internal static void ApplyTo(this SubdivisionFieldsDto fields, Subdivision entity) {
    entity.Name = fields.Name;
    entity.PositionFormationName = fields.PositionFormationName;
    entity.PostalCode = fields.PostalCode;
    entity.Address = fields.Address;
    entity.Phone = fields.Phone;
    entity.Fax = fields.Fax;
    entity.IsDepartment = fields.IsDepartment;
    entity.StaffCount = fields.StaffCount;
    entity.LevelOrder = fields.LevelOrder;
    entity.StatusType = fields.StatusType;
    entity.StatusCode = fields.StatusCode;
    entity.OrganTypeId = fields.OrganTypeId;
    entity.OrganTypeCode = fields.OrganTypeCode;
  }

  internal static ClassifierFieldsDto From(Classifier entity) => new() {
    Type = entity.Type,
    Code = entity.Code,
    ClassifierName = entity.ClassifierName,
    Value = entity.Value,
    IsActive = entity.IsActive
  };

  internal static void ApplyTo(this ClassifierFieldsDto fields, Classifier entity) {
    entity.Type = fields.Type;
    entity.Code = fields.Code;
    entity.ClassifierName = fields.ClassifierName;
    entity.Value = fields.Value;
    entity.IsActive = fields.IsActive;
  }
}
