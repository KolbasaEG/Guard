using Guard.Core.Services.DTOs;
namespace Guard.Core.Services;
public interface IRoleAccessService
{
  Task<RoleEditDto> GetAsync(string id, CancellationToken ct = default);
  Task<string> SaveAsync(RoleEditDto input, CancellationToken ct = default);
  Task DeleteAsync(string id, CancellationToken ct = default);
  Task<UserRoleAssignmentsDto> GetUserRolesAsync(string userId, CancellationToken ct = default);
  Task SetUserRolesAsync(string userId, IEnumerable<string> roles, string version, CancellationToken ct = default);
  Task<PermissionTransitionReport> GetTransitionReportAsync(CancellationToken ct = default);
  Task InitializeAdministratorAsync(CancellationToken ct = default);
}
