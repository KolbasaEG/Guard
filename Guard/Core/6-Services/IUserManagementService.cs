using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.AspNetCore.Identity;
using Radzen;

namespace Guard.Core.Services;

public interface IUserManagementService
{
  // === Пользователи ===
  Task<List<ApplicationUser>> GetAllUsersAsync();
  IQueryable<ApplicationUser> GetAllUsersAsQueryable();
  Task<ApplicationUser?> GetUserByIdAsync(string userId);
  Task<IdentityResult> CreateUserAsync(ApplicationUser user, string password);
  Task<IdentityResult> UpdateUserAsync(ApplicationUser user);
  Task<IdentityResult> DeleteUserAsync(string userId);

  // === Роли пользователя ===
  Task<IdentityResult> CreateRoleAsync(string roleName);
  Task<IdentityResult> UpdateRoleAsync(ApplicationRole role);
  Task<IdentityResult> DeleteRoleAsync(string roleName);
  IQueryable<ApplicationRole> GetAllRolesAsQueryable();
  Task<List<ApplicationRole>> GetAllRolesAsync();
  Task<IList<string>> GetUserRolesAsync(string userId);
  Task<IdentityResult> AssignRoleToUserAsync(string userId, string roleName);
  Task<IdentityResult> RemoveRoleFromUserAsync(string userId, string roleName);

  // === IP-адреса (справочник) ===
  Task<List<IpAddress>> GetAllIpAddressesAsync(CancellationToken ct = default);
  Task<(List<IpAddress> Items, int TotalCount)> GetIpAddressesAsync(int skip, int top, string? orderBy = null, IEnumerable<CompositeFilterDescriptor>? filters = null, Status? status = null, CancellationToken ct = default);
  Task<IpAddress?> GetIpAddressByIdAsync(Guid id, CancellationToken ct = default);
  Task<IpAddress> CreateIpAddressAsync(string address, string name, string? description = null, CancellationToken ct = default);
  Task UpdateIpAddressAsync(IpAddress ipAddress, CancellationToken ct = default);
  Task DeleteIpAddressAsync(Guid id, string modifiedBy, CancellationToken ct = default);
  Task SoftDeleteIpAddressAsync(Guid id, CancellationToken ct = default);
  Task ArchiveIpAddressAsync(Guid id, CancellationToken ct = default);
  Task RestoreIpAddressAsync(Guid id, CancellationToken ct = default);
  // === Назначение IP пользователю ===
  Task AssignIpToUserAsync(string userId, Guid ipAddressId, string assignedBy, string? purpose = "Login", string? description = null, CancellationToken ct = default);
  Task RemoveIpFromUserAsync(string userId, Guid ipAddressId, CancellationToken ct = default);
}