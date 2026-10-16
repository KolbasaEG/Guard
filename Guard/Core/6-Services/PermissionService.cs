using Guard.Core.Contexts;
using Guard.Core.Identity;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public class PermissionService(ICurrentSession session,
    IDbContextFactory<ApplicationDbContext> factory, IAuditService audit, IAccountPolicyService policies) : IPermissionService
{
  public async Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default)
  {
    var current = await session.GetAsync(ct);
    if (current == null)
      throw new UnauthorizedAccessException("Необходим вход в систему.");
    var id = current.UserId;
    await using var db = await factory.CreateDbContextAsync(ct);
    var stamp = await db.Users.Where(u => u.Id == id).Select(u => u.SecurityStamp).SingleOrDefaultAsync(ct);
    if (stamp == null || stamp != current.SecurityStamp)
      throw new UnauthorizedAccessException("Сессия завершена. Войдите заново.");
    return await GetForUserAsync(id, ct);
  }

  public async Task<UserAccessSnapshot> GetForUserAsync(string userId, CancellationToken ct = default)
  {
    var state = await policies.CheckAsync(userId, ct);
    if (state.Blocked || state.PasswordExpired) throw new UnauthorizedAccessException("Учётная запись требует смены пароля или заблокирована.");
    await using var db = await factory.CreateDbContextAsync(ct);
    if (!await db.Users.AnyAsync(u => u.Id == userId && u.AccountBlockReason == null &&
        (!u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow), ct))
      throw new UnauthorizedAccessException("Учётная запись недоступна.");
    var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                       where ur.UserId == userId select new { r.Id, r.NormalizedName }).ToListAsync(ct);
    var ids = roles.Select(r => r.Id).ToArray();
    var codes = await db.RoleClaims.Where(c => ids.Contains(c.RoleId) && c.ClaimType == "Permission")
        .Select(c => c.ClaimValue!).ToListAsync(ct);
    // Индивидуальные claims не участвуют в ролевой модели; решение документируется при переходе.
    return new(userId, roles.Any(r => r.NormalizedName == "ROOT"),
        codes.Where(c => PermissionCatalog.All.Any(p => p.Code == c)).ToHashSet(StringComparer.Ordinal));
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
