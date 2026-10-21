using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.EntityFrameworkCore;

public class ReadRepository<T>(IDbContextFactory<ApplicationDbContext> factory,
    IPermissionService permissions, IDataAccessScopeService scopes, ILogger<ReadRepository<T>>? logger = null) : IReadRepository<T> where T : class
{
  public async Task<TResult> QueryAsync<TResult>(Func<IQueryable<T>, Task<TResult>> query, CancellationToken ct = default)
  {
    var elapsed = System.Diagnostics.Stopwatch.StartNew();
    using var scope = logger?.BeginScope(new Dictionary<string, object> { ["Operation"] = "RepositoryRead", ["EntityType"] = typeof(T).Name });
    logger?.LogDebug(new EventId(5101, "ReadStarted"), "Начало чтения {EntityType}", typeof(T).Name);
    try {
      var result = await QueryCoreAsync(query, ct);
      logger?.LogDebug(new EventId(5102, "ReadCompleted"), "Чтение {EntityType} выполнено за {DurationMs} мс", typeof(T).Name, elapsed.Elapsed.TotalMilliseconds);
      return result;
    } catch (OperationCanceledException) when (ct.IsCancellationRequested) {
      logger?.LogDebug(new EventId(5103, "ReadCancelled"), "Чтение {EntityType} отменено за {DurationMs} мс", typeof(T).Name, elapsed.Elapsed.TotalMilliseconds); throw;
    } catch (Exception error) {
      // Ошибка будет зарегистрирована вызывающей операцией; подробная диагностика добавляет этап без второго Error.
      logger?.LogDebug(new EventId(5104, "ReadFailed"), error, "Чтение {EntityType} прервано за {DurationMs} мс", typeof(T).Name, elapsed.Elapsed.TotalMilliseconds); throw;
    }
  }
  private async Task<TResult> QueryCoreAsync<TResult>(Func<IQueryable<T>, Task<TResult>> query, CancellationToken ct)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    var access = await permissions.GetCurrentAsync(ct);
    access = access with { Permissions = new(access.Permissions) };
    DataAccessScope? initialScope = null;
    IQueryable<T> source = db.Set<T>().AsNoTracking();
    if (typeof(T) == typeof(Personal))
    {
      await permissions.RequireAsync(Permissions.Personals.ReadDetails, ct);
      var scope = await scopes.GetAsync(ct);
      initialScope = scope with { SubdivisionIds = new(scope.SubdivisionIds) };
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
      initialScope = scope with { SubdivisionIds = new(scope.SubdivisionIds) };
      var q = (IQueryable<Subdivision>)source;
      if (!scope.IsRoot) q = q.Where(s => scope.SubdivisionIds.Contains(s.Id) && s.Status != Status.Deleted);
      if (!access.Has(Permissions.Subdivisions.ReadArchive)) q = q.Where(s => s.Status != Status.Archived && s.Status != Status.ArchivedBlocked);
      source = (IQueryable<T>)q;
    }
    else if (typeof(T) == typeof(ApplicationUser))
    {
      await permissions.RequireAsync(Permissions.Users.Read, ct);
      var scope = await scopes.GetAsync(ct);
      initialScope = scope with { SubdivisionIds = new(scope.SubdivisionIds) };
      var q = (IQueryable<ApplicationUser>)source;
      if (!scope.IsRoot) q = q.Where(u => u.Personal != null && u.Personal.SubdivisionId.HasValue &&
          scope.SubdivisionIds.Contains(u.Personal.SubdivisionId.Value));
      source = (IQueryable<T>)q;
    }
    else if (typeof(T) == typeof(ApplicationRole)) await permissions.RequireAsync(Permissions.Roles.Read, ct);
    else if (typeof(T) == typeof(Classifier)) await permissions.RequireAsync(Permissions.Classifiers.Read, ct);
    else if (typeof(T) == typeof(IpAddress))
    {
      await permissions.RequireAsync(Permissions.IpAddresses.Read, ct);
      var scope = await scopes.GetAsync(ct);
      initialScope = scope with { SubdivisionIds = new(scope.SubdivisionIds) };
      var q = (IQueryable<IpAddress>)source;
      if (!scope.IsRoot) q = q.Where(ip => ip.SubdivisionId.HasValue && scope.SubdivisionIds.Contains(ip.SubdivisionId.Value));
      source = (IQueryable<T>)q;
    }
    else throw new InvalidOperationException("Для сущности не определена политика чтения.");
    var result = await query(source);
    var current = await permissions.GetCurrentAsync(ct);
    if (current.UserId != access.UserId || current.IsRoot != access.IsRoot || !current.Permissions.SetEquals(access.Permissions))
      throw new UnauthorizedAccessException("Доступ изменился. Повторите загрузку.");
    if (initialScope != null) {
      var scope = await scopes.GetAsync(ct);
      if (scope.UserId != initialScope.UserId || scope.IsRoot != initialScope.IsRoot || !scope.SubdivisionIds.SetEquals(initialScope.SubdivisionIds))
        throw new UnauthorizedAccessException("Область доступа изменилась. Повторите загрузку.");
    }
    return result;
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
