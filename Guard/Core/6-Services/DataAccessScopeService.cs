using Guard.Core.Contexts;
using Microsoft.EntityFrameworkCore;
namespace Guard.Core.Services;

public class DataAccessScopeService(IPermissionService permissions, IDbContextFactory<ApplicationDbContext> factory)
    : IDataAccessScopeService
{
  private static bool ValidPath(string? path) => path != null && path.StartsWith('/') && path.EndsWith('/') &&
    path.Split('/', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } segments &&
    segments.All(s => long.TryParse(s, out var value) && value > 0);
  public async Task<DataAccessScope> GetAsync(CancellationToken ct = default)
  {
    var user = await permissions.GetCurrentAsync(ct);
    if (user.IsRoot) return new(user.UserId, true, []);
    await using var db = await factory.CreateDbContextAsync(ct);
    var path = await db.Users.Where(u => u.Id == user.UserId)
        .Select(u => u.Personal == null || u.Personal.Subdivision == null ? null : u.Personal.Subdivision.Path)
        .SingleOrDefaultAsync(ct);
    var ids = !ValidPath(path) ? new List<Guid>() :
        await db.Subdivisions.Where(s => s.Path.StartsWith(path!)).Select(s => s.Id).ToListAsync(ct);
    return new(user.UserId, false, ids.ToHashSet());
  }
}
