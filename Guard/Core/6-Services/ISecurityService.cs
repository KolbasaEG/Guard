using Guard.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Guard.Core.Services;

/// <summary>
/// Сервис управления системой безопасности Guard.
/// Предоставляет универсальные методы Query...Async для гибкого выполнения LINQ-запросов,
/// включая фильтрацию, сортировку и пагинацию в Radzen DataGrid.
/// </summary>
public interface ISecurityService
{
  #region Гибкие методы чтения (Querying)

  /// <summary>
  /// Выполняет произвольный LINQ-запрос к пользователям (ApplicationUser).
  /// </summary>
  Task<TResult> QueryUsersAsync<TResult>(
      Func<IQueryable<ApplicationUser>, Task<TResult>> query,
      CancellationToken ct = default);

  /// <summary>
  /// Выполняет произвольный LINQ-запрос к ролям (ApplicationRole).
  /// </summary>
  Task<TResult> QueryRolesAsync<TResult>(
      Func<IQueryable<ApplicationRole>, Task<TResult>> query,
      CancellationToken ct = default);

  /// <summary>
  /// Выполняет произвольный LINQ-запрос к IP-адресам (IpAddress).
  /// </summary>
  Task<TResult> QueryIpAddressesAsync<TResult>(
      Func<IQueryable<IpAddress>, Task<TResult>> query,
      CancellationToken ct = default);

  /// <summary>
  /// Выполняет произвольный LINQ-запрос к сотрудникам (Personal).
  /// </summary>
  Task<TResult> QueryPersonalsAsync<TResult>(
      Func<IQueryable<Personal>, Task<TResult>> query,
      CancellationToken ct = default);

  #endregion

  #region Операции с пользователями

  Task<ApplicationUser?> GetUserByIdAsync(string id, CancellationToken ct = default);
  Task<IdentityResult> CreateUserAsync(ApplicationUser user, string password, IEnumerable<string>? roles = null, CancellationToken ct = default);
  Task<IdentityResult> UpdateUserAsync(ApplicationUser user, IEnumerable<string> roles, CancellationToken ct = default);
  Task<IdentityResult> ToggleUserLockoutAsync(string userId, bool lockout, CancellationToken ct = default);
  Task<IdentityResult> ResetPasswordAsync(string userId, string newPassword, CancellationToken ct = default);

  #endregion

  #region Роли и Права (Claims)

  Task<List<string>> GetUserRolesAsync(string userId, CancellationToken ct = default);
  Task<List<string>> GetRoleClaimsAsync(string roleId, CancellationToken ct = default);
  Task<IdentityResult> UpdateRoleClaimsAsync(string roleId, IEnumerable<string> permissions, CancellationToken ct = default);
  Task<List<string>> GetUserClaimsAsync(string userId, CancellationToken ct = default);
  Task<IdentityResult> UpdateUserClaimsAsync(string userId, IEnumerable<string> permissions, CancellationToken ct = default);

  #endregion

  #region IP-Адреса и Доступ

  Task UpdatePersonalIpAddressesAsync(Guid personalId, IEnumerable<Guid> ipAddressIds, CancellationToken ct = default);
  Task<bool> IsIpAllowedForUserAsync(string userId, string clientIp, CancellationToken ct = default);

  #endregion
}