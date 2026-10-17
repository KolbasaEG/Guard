using Guard.Core.Identity;
using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

public class PermissionService(ICurrentSession session,
    IPermissionReader reader, IAuditService audit, IAccountPolicyService policies) : IPermissionService
{
  public async Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default)
  {
    var current = await session.GetAsync(ct);
    if (current == null)
      throw new UnauthorizedAccessException("Необходим вход в систему.");
    var id = current.UserId;
    var stamp = await reader.GetSecurityStampAsync(id, ct);
    if (stamp == null || stamp != current.SecurityStamp)
      throw new UnauthorizedAccessException("Сессия завершена. Войдите заново.");
    return await GetForUserAsync(id, ct);
  }

  public async Task<UserAccessSnapshot> GetForUserAsync(string userId, CancellationToken ct = default)
  {
    var state = await policies.CheckAsync(userId, ct);
    if (state.Blocked || state.PasswordExpired) throw new UnauthorizedAccessException("Учётная запись требует смены пароля или заблокирована.");
    var access = await reader.FindAccessAsync(userId, ct);
    if (access == null)
      throw new UnauthorizedAccessException("Учётная запись недоступна.");
    // Индивидуальные claims не участвуют в ролевой модели; решение документируется при переходе.
    return new(userId, access.RoleNames.Any(name => name == "ROOT"),
        access.PermissionCodes.Where(c => c != null && PermissionCatalog.All.Any(p => p.Code == c))
          .Select(c => c!).ToHashSet(StringComparer.Ordinal));
  }
  public async Task<bool> HasAsync(string permission, CancellationToken ct = default)
  {
    try { return (await GetCurrentAsync(ct)).Has(permission); }
    catch (UnauthorizedAccessException) { return false; }
  }
  public async Task RequireAsync(string permission, CancellationToken ct = default)
  {
    if (!await HasAsync(permission, ct)) {
      var id = (await session.GetAsync(ct))?.UserId;
      try { id = (await GetCurrentAsync(ct)).UserId; } catch (UnauthorizedAccessException) { }
      audit.LogIdentityEvent(Guard.Core.Enums.AuditEventType.AccessDenied, id, details: $"Permission={permission}");
      throw new UnauthorizedAccessException("Недостаточно прав для этой операции.");
    }
  }
}
