namespace Guard.Core.Services.DTOs;
public class RoleEditDto
{
  public string Id { get; set; } = "";
  public string Name { get; set; } = "";
  public string? Version { get; set; }
  public bool IsSystem { get; set; }
  public HashSet<string> Permissions { get; set; } = [];
}
public record RoleOptionDto(string Id, string Name);
public record UserRoleAssignmentsDto(IReadOnlyList<RoleOptionDto> Available, IReadOnlyList<string> Selected, string Version);
public record PermissionTransitionReport(int IndividualPermissionCount, IReadOnlyList<string> UnconfiguredRoles);
