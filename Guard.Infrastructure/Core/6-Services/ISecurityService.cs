using Guard.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Guard.Core.Services;

/// <summary>
/// Сервис управления системой безопасности Guard.
/// Успешная запись подтверждает commit; последующие ошибки доставки аудита и уведомлений логируются.
/// Переходный контракт: оставшиеся entity-операции используются интеграционными проверками Identity.
/// </summary>
public interface ISecurityService : IUserAdministrationService, IUserListService
{
  #region Операции с пользователями

  Task<ApplicationUser?> GetUserByIdAsync(string id, CancellationToken ct = default);
  Task<IdentityResult> UpdateUserDetailsAsync(Guard.Core.Services.DTOs.EditUserDto input, CancellationToken ct = default);
  Task<IdentityResult> CreateUserAsync(ApplicationUser user, string password, IEnumerable<string>? roles = null, CancellationToken ct = default);
  Task<IdentityResult> UpdateUserAsync(ApplicationUser user, IEnumerable<string> roles, CancellationToken ct = default);
  Task<IdentityResult> ToggleUserLockoutAsync(string userId, bool lockout, CancellationToken ct = default);
  Task<IdentityResult> ResetPasswordAsync(string userId, string newPassword, CancellationToken ct = default);

  #endregion

}
