using Guard.Core.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public sealed class DataAccessScopeReader(IDbContextFactory<ApplicationDbContext> factory) : IDataAccessScopeReader
{
  public async Task<string?> FindUserPathAsync(string userId, CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    return await db.Users.AsNoTracking().Where(u => u.Id == userId)
      .Select(u => u.Personal == null || u.Personal.Subdivision == null ? null : u.Personal.Subdivision.Path)
      .SingleOrDefaultAsync(ct);
  }

  public async Task<IReadOnlyList<Guid>> GetSubdivisionIdsAsync(string path, CancellationToken ct = default)
  {
    // Даже прямой вызов порта не должен выполнять выборку по пустому префиксу.
    if (!SubdivisionAccessPath.IsValid(path))
      throw new ArgumentException("Некорректный путь подразделения.", nameof(path));
    await using var db = await factory.CreateDbContextAsync(ct);
    return await db.Subdivisions.AsNoTracking().Where(s => s.Path.StartsWith(path))
      .Select(s => s.Id).ToListAsync(ct);
  }
}
