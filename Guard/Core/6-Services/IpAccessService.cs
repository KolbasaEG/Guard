using Guard.Core.Contexts;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public class IpAccessService(IDbContextFactory<ApplicationDbContext> factory) : IIpAccessService
{
  public async Task<bool> IsAllowedAsync(string userId, string? clientIp, CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    var user = await db.Users.AsNoTracking().Where(u => u.Id == userId)
        .Select(u => new { u.PersonalId, PersonalStatus = u.Personal == null ? (Status?)null : u.Personal.Status })
        .SingleOrDefaultAsync(ct);
    if (user == null) return false;
    var root = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
                      where ur.UserId == userId && role.NormalizedName == "ROOT" select ur.UserId).AnyAsync(ct);
    if (root) return true;
    if (user.PersonalId == null || user.PersonalStatus is not (Status.Inserted or Status.Modified) ||
        !IpAddressRule.TryParseClient(clientIp, out var address)) return false;
    var rules = await db.Personals.Where(p => p.Id == user.PersonalId).SelectMany(p => p.IpAddresses)
        .Where(ip => ip.Status == Status.Inserted || ip.Status == Status.Modified)
        .Select(ip => ip.Address).ToListAsync(ct);
    return rules.Any(value => IpAddressRule.TryParse(value, out var rule) && rule!.Contains(address!));
  }
}
