using Guard.Core.Services.DTOs;
namespace Guard.Core.Services;
public interface IRoleAccessService
{
  // Успех записи означает подтверждённый commit. Ошибки последующего уведомления
  // и доставки аудита регистрируются техническим логгером, без повторной записи.
  Task<RoleEditDto> GetAsync(string id, CancellationToken ct = default);
  Task<string> SaveAsync(RoleEditDto input, CancellationToken ct = default);
  Task DeleteAsync(string id, CancellationToken ct = default);
  Task<UserRoleAssignmentsDto> GetUserRolesAsync(string userId, CancellationToken ct = default);
  Task SetUserRolesAsync(string userId, IEnumerable<string> roles, string version, CancellationToken ct = default);
  Task<PermissionTransitionReport> GetTransitionReportAsync(CancellationToken ct = default);
  Task InitializeAdministratorAsync(CancellationToken ct = default);
}
