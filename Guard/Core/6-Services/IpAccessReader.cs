using Guard.Core.Contexts;
using Guard.Core.Enums;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

// Infrastructure: each read owns and disposes its context; no deferred queries escape.
public sealed class IpAccessReader(IDbContextFactory<ApplicationDbContext> factory) : IIpAccessReader
{
  public async Task<IpAccessSubject?> FindSubjectAsync(string userId, CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    var user = await db.Users.AsNoTracking().Where(u => u.Id == userId)
      .Select(u => new { u.PersonalId, PersonalStatus = u.Personal == null ? (Status?)null : u.Personal.Status })
      .SingleOrDefaultAsync(ct);
    if (user == null) return null;
    var root = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
      where ur.UserId == userId && role.NormalizedName == "ROOT" select ur.UserId).AnyAsync(ct);
    return new(root, user.PersonalId, user.PersonalStatus);
  }

  public async Task<IReadOnlyList<string>> GetActiveRulesAsync(Guid personalId, CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    return await db.Personals.AsNoTracking().Where(p => p.Id == personalId).SelectMany(p => p.IpAddresses)
      .Where(ip => ip.Status == Status.Inserted || ip.Status == Status.Modified)
      .Select(ip => ip.Address).ToListAsync(ct);
  }
}
