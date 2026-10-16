using Guard.Core.Enums;

namespace Guard.Core.Services.DTOs;

public sealed record SubdivisionDetailsDto
{
  public Guid Id { get; init; }
  public Guid Version { get; init; }
  public Status Status { get; set; } = Status.Inserted;
  public Guid? ParentId { get; set; }
  public string? ParentName { get; init; }
  public string? OrganTypeName { get; init; }
  public string? StatusClassifierName { get; init; }
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
  public EntityInfoDto Info { get; init; } = new();

  public SubdivisionFieldsDto ToFields() => new() {
    Name = Name, PositionFormationName = PositionFormationName, PostalCode = PostalCode,
    Address = Address, Phone = Phone, Fax = Fax, IsDepartment = IsDepartment,
    StaffCount = StaffCount, LevelOrder = LevelOrder, StatusType = StatusType, StatusCode = StatusCode,
    OrganTypeId = OrganTypeId, OrganTypeCode = OrganTypeCode
  };
}

public sealed record SubdivisionParentOptionDto(Guid Id, string Name, string Path);
public sealed record SubdivisionClassifierOptionDto(int Type, int Code, string Value);
public sealed record SubdivisionEditorOptionsDto(
  IReadOnlyList<SubdivisionParentOptionDto> Parents,
  IReadOnlyList<SubdivisionClassifierOptionDto> OrganTypes,
  IReadOnlyList<SubdivisionClassifierOptionDto> StatusClassifiers);
