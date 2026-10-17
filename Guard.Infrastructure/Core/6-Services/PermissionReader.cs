using Guard.Core.Contexts;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public sealed class PermissionReader(IDbContextFactory<ApplicationDbContext> factory) : IPermissionReader
{
  public async Task<string?> GetSecurityStampAsync(string userId, CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    return await db.Users.AsNoTracking().Where(u => u.Id == userId)
      .Select(u => u.SecurityStamp).SingleOrDefaultAsync(ct);
  }

  public async Task<PermissionAccessData?> FindAccessAsync(string userId, CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    if (!await db.Users.AnyAsync(u => u.Id == userId && u.AccountBlockReason == null &&
        (!u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow), ct))
      return null;
    var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
                       where ur.UserId == userId select new { r.Id, r.NormalizedName }).ToListAsync(ct);
    var ids = roles.Select(r => r.Id).ToArray();
    // Индивидуальные claims не участвуют в ролевой модели.
    var codes = await db.RoleClaims.Where(c => ids.Contains(c.RoleId) && c.ClaimType == "Permission")
      .Select(c => c.ClaimValue).ToListAsync(ct);
    return new(roles.Select(r => r.NormalizedName).ToArray(), codes);
  }
}
