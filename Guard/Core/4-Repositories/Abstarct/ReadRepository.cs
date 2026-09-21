using Guard.Core.Contexts;
using Microsoft.EntityFrameworkCore;

public class ReadRepository<T> : IReadRepository<T> where T : class
{
  private readonly IDbContextFactory<ApplicationDbContext> _factory;

  public ReadRepository(IDbContextFactory<ApplicationDbContext> factory)
  {
    _factory = factory;
  }

  public async Task<TResult> QueryAsync<TResult>(Func<IQueryable<T>, Task<TResult>> query, CancellationToken ct = default)
  {
    await using var context = await _factory.CreateDbContextAsync(ct);
    return await query(context.Set<T>().AsNoTracking());
  }
  public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
  {
    await using var context = await _factory.CreateDbContextAsync(ct);

    return await context.Set<T>()
        .AsNoTracking()
        .ToListAsync(ct);
  }
  public async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
  {
    await using var context = await _factory.CreateDbContextAsync(ct);

    // FindAsync автоматически определяет имя и тип первичного ключа любой таблицы
    return await context.Set<T>().FindAsync(new[] { id }, ct);
  }
  public async Task<bool> ExistsAsync(object id, CancellationToken ct = default)
  {
    return await GetByIdAsync(id, ct) is not null;
  }
  public async Task<int> CountAsync(CancellationToken ct = default)
  {
    await using var context = await _factory.CreateDbContextAsync(ct);

    return await context.Set<T>()
        .AsNoTracking()
        .CountAsync(ct);
  }
}