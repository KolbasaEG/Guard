using Guard.Core.Contexts;
using Guard.Core.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Guard.Core.Services;

public class IpManagementAccessService(AuthenticationStateProvider authentication,
    IDbContextFactory<ApplicationDbContext> factory) : IIpManagementAccessService
{
  public async Task<IpManagementScope> GetScopeAsync(bool write, CancellationToken ct = default)
  {
    var principal = (await authentication.GetAuthenticationStateAsync()).User;
    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (principal.Identity?.IsAuthenticated != true || userId == null)
      throw new UnauthorizedAccessException("Необходим вход в систему.");
    await using var db = await factory.CreateDbContextAsync(ct);
    // Проверяем актуальные роли и claims в БД, а не только устаревающие claims cookie.
    var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                       where ur.UserId == userId select new { r.Id, r.NormalizedName }).ToListAsync(ct);
    var root = roles.Any(r => r.NormalizedName == "ROOT");
    var roleIds = roles.Select(r => r.Id).ToArray();
    var permission = write ? Permissions.Users.Manage : Permissions.Users.Read;
    var personalPermission = write ? Permissions.Personals.Write : Permissions.Personals.Read;
    var permitted = root || roles.Any(r => r.NormalizedName == "АДМИНИСТРАТОР") ||
        await db.UserClaims.AnyAsync(c => c.UserId == userId && c.ClaimType == "Permission" &&
            (c.ClaimValue == permission || c.ClaimValue == personalPermission), ct) ||
        await db.RoleClaims.AnyAsync(c => roleIds.Contains(c.RoleId) && c.ClaimType == "Permission" &&
            (c.ClaimValue == permission || c.ClaimValue == personalPermission), ct);
    if (!permitted) throw new UnauthorizedAccessException("Недостаточно прав для управления IP-адресами.");
    var path = await db.Users.Where(u => u.Id == userId)
        .Select(u => u.Personal == null || u.Personal.Subdivision == null ? null : u.Personal.Subdivision.Path)
        .SingleOrDefaultAsync(ct);
    var ids = root || string.IsNullOrWhiteSpace(path) ? new List<Guid>() :
        await db.Subdivisions.Where(s => s.Path.StartsWith(path!)).Select(s => s.Id).ToListAsync(ct);
    return new IpManagementScope(userId, root, ids.ToHashSet());
  }
}
