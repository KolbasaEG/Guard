using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.EntityFrameworkCore;

public class ReadRepository<T>(IDbContextFactory<ApplicationDbContext> factory,
    IPermissionService permissions, IDataAccessScopeService scopes) : IReadRepository<T> where T : class
{
  public async Task<TResult> QueryAsync<TResult>(Func<IQueryable<T>, Task<TResult>> query, CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    var access = await permissions.GetCurrentAsync(ct);
    IQueryable<T> source = db.Set<T>().AsNoTracking();
    if (typeof(T) == typeof(Personal))
    {
      await permissions.RequireAsync(Permissions.Personals.ReadDetails, ct);
      var scope = await scopes.GetAsync(ct);
      var q = (IQueryable<Personal>)source;
      if (!scope.IsRoot) q = q.Where(p => p.SubdivisionId.HasValue && scope.SubdivisionIds.Contains(p.SubdivisionId.Value));
      if (!access.Has(Permissions.Personals.ReadArchive)) q = q.Where(p => p.Status != Status.Archived && p.Status != Status.ArchivedBlocked);
      if (!scope.IsRoot) q = q.Where(p => p.Status != Status.Deleted);
      source = (IQueryable<T>)q;
    }
    else if (typeof(T) == typeof(Subdivision))
    {
      await permissions.RequireAsync(Permissions.Subdivisions.Read, ct);
      var scope = await scopes.GetAsync(ct);
      var q = (IQueryable<Subdivision>)source;
      if (!scope.IsRoot) q = q.Where(s => scope.SubdivisionIds.Contains(s.Id) && s.Status != Status.Deleted);
      if (!access.Has(Permissions.Subdivisions.ReadArchive)) q = q.Where(s => s.Status != Status.Archived && s.Status != Status.ArchivedBlocked);
      source = (IQueryable<T>)q;
    }
    else if (typeof(T) == typeof(ApplicationUser))
    {
      await permissions.RequireAsync(Permissions.Users.Read, ct);
      var scope = await scopes.GetAsync(ct);
      var q = (IQueryable<ApplicationUser>)source;
      if (!scope.IsRoot) q = q.Where(u => u.Personal != null && u.Personal.SubdivisionId.HasValue &&
          scope.SubdivisionIds.Contains(u.Personal.SubdivisionId.Value));
      source = (IQueryable<T>)q;
    }
    else if (typeof(T) == typeof(ApplicationRole)) await permissions.RequireAsync(Permissions.Roles.Read, ct);
    else if (typeof(T) == typeof(Classifier)) await permissions.RequireAsync(Permissions.Classifiers.Read, ct);
    else if (typeof(T) == typeof(OrganType)) await permissions.RequireAsync(Permissions.OrganTypes.Read, ct);
    else if (typeof(T) == typeof(IpAddress))
    {
      await permissions.RequireAsync(Permissions.IpAddresses.Read, ct);
      var scope = await scopes.GetAsync(ct);
      var q = (IQueryable<IpAddress>)source;
      if (!scope.IsRoot) q = q.Where(ip => ip.SubdivisionId.HasValue && scope.SubdivisionIds.Contains(ip.SubdivisionId.Value));
      source = (IQueryable<T>)q;
    }
    else throw new InvalidOperationException("Для сущности не определена политика чтения.");
    return await query(source);
  }
  public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default) => await QueryAsync(q => q.ToListAsync(ct), ct);
  public async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
  {
    return await QueryAsync(async q =>
    {
      // В проекте используются только Guid, int и string Id; фильтрация выполняется в SQL.
      if (id is Guid guid) return await q.FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == guid, ct);
      if (id is int number) return await q.FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == number, ct);
      if (id is string value) return await q.FirstOrDefaultAsync(e => EF.Property<string>(e, "Id") == value, ct);
      throw new ArgumentException("Неподдерживаемый тип идентификатора.");
    }, ct);
  }
  public async Task<bool> ExistsAsync(object id, CancellationToken ct = default) => await GetByIdAsync(id, ct) != null;
  public Task<int> CountAsync(CancellationToken ct = default) => QueryAsync(q => q.CountAsync(ct), ct);
}
