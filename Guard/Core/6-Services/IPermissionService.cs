using Guard.Core.Services.DTOs;
namespace Guard.Core.Services;

public interface IPermissionService
{
  Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default);
  Task<UserAccessSnapshot> GetForUserAsync(string userId, CancellationToken ct = default);
  Task<bool> HasAsync(string permission, CancellationToken ct = default);
  Task RequireAsync(string permission, CancellationToken ct = default);
}
