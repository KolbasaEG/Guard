using Guard.Core.Enums;

namespace Guard.Core.Services.DTOs;

public sealed record SubdivisionListDto
{
  public Guid Id { get; init; }
  public Guid Version { get; init; }
  public Status Status { get; init; }
  public string Name { get; init; } = "";
  public string? OrganTypeName { get; init; }
  public string? StatusClassifierName { get; init; }
  public string? ParentName { get; init; }
  public string? PositionFormationName { get; init; }
  public string? PostalCode { get; init; }
  public string? Address { get; init; }
  public string? Phone { get; init; }
  public double StaffCount { get; init; }
  public int LevelOrder { get; init; }
  public bool IsDepartment { get; init; }
}
public sealed record ClassifierListDto(int Id, Guid Version, int Type, int Code, string ClassifierName,
    string Value, bool IsActive, DateTime UpdatedAt);
public sealed record OrganTypeListDto(int Id, Guid Version, int Code, string Name);
public sealed record RoleListDto(string Id, string? Name, string? NormalizedName);
