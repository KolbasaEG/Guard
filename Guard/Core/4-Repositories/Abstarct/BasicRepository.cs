using Guard.Core.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Repositories;

/// <summary>
/// Реализация базового репозитория для любых CLR-классов сущностей EF Core.
/// </summary>
public class BasicRepository<T> : IBasicRepository<T> where T : class
{
  protected readonly ApplicationDbContext Context;
  protected readonly DbSet<T> DbSet;

  public BasicRepository(ApplicationDbContext context)
  {
    Context = context;
    DbSet = context.Set<T>();
  }

  // ==================== IQueryable ====================
  public IQueryable<T> Query() => DbSet.AsQueryable();

  // ==================== Read ====================
  public T? GetById(params object[] keyValues) => DbSet.Find(keyValues);

  public async Task<T?> GetByIdAsync(object[] keyValues, CancellationToken ct = default)
      => await DbSet.FindAsync(keyValues, ct);

  public IReadOnlyList<T> GetAll()
      => DbSet.AsNoTracking().ToList();

  public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
      => await DbSet.AsNoTracking().ToListAsync(ct);

  // ==================== Create ====================
  public void Add(T entity) => DbSet.Add(entity);

  public async Task AddAsync(T entity, CancellationToken ct = default)
      => await DbSet.AddAsync(entity, ct);

  public void AddRange(IEnumerable<T> entities) => DbSet.AddRange(entities);

  public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
      => await DbSet.AddRangeAsync(entities, ct);

  // ==================== Update ====================
  public void Update(T entity)
  {
    ArgumentNullException.ThrowIfNull(entity);

    var trackedEntry = FindTrackedEntry(entity);

    if (trackedEntry != null)
    {
      if (!ReferenceEquals(trackedEntry.Entity, entity))
      {
        trackedEntry.CurrentValues.SetValues(entity);
      }
    }
    else
    {
      DbSet.Update(entity);
    }
  }

  public async Task UpdateAsync(T entity, CancellationToken ct = default)
  {
    Update(entity);
    await Task.CompletedTask;
  }

  public void UpdateRange(IEnumerable<T> entities)
  {
    foreach (var entity in entities)
    {
      Update(entity);
    }
  }

  public async Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
  {
    UpdateRange(entities);
    await Task.CompletedTask;
  }

  // ==================== Delete ====================
  public void Delete(T entity)
  {
    ArgumentNullException.ThrowIfNull(entity);

    var trackedEntry = FindTrackedEntry(entity);
    if (trackedEntry != null)
    {
      DbSet.Remove(trackedEntry.Entity);
    }
    else
    {
      DbSet.Remove(entity);
    }
  }

  public async Task DeleteAsync(T entity, CancellationToken ct = default)
  {
    Delete(entity);
    await Task.CompletedTask;
  }

  public void DeleteRange(IEnumerable<T> entities) => DbSet.RemoveRange(entities);

  public async Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
  {
    DeleteRange(entities);
    await Task.CompletedTask;
  }

  // ==================== Дополнительные методы ====================
  public bool Exists(params object[] keyValues) => DbSet.Find(keyValues) != null;

  public async Task<bool> ExistsAsync(object[] keyValues, CancellationToken ct = default)
      => await DbSet.FindAsync(keyValues, ct) != null;

  public int Count() => DbSet.Count();

  public async Task<int> CountAsync(CancellationToken ct = default)
      => await DbSet.CountAsync(ct);

  // ==================== Вспомогательные методы ====================

  /// <summary>
  /// Динамически находит отслеживаемую сущность в ChangeTracker по ее первичному ключу (включая составные ключи).
  /// </summary>
  private Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T>? FindTrackedEntry(T entity)
  {
    var entityType = Context.Model.FindEntityType(typeof(T));
    var primaryKey = entityType?.FindPrimaryKey();

    if (primaryKey == null) return null;

    var keyProperties = primaryKey.Properties;
    var targetKeyValues = keyProperties.Select(p => p.PropertyInfo?.GetValue(entity)).ToArray();

    return Context.ChangeTracker.Entries<T>().FirstOrDefault(entry =>
    {
      var currentKeyValues = keyProperties.Select(p => p.PropertyInfo?.GetValue(entry.Entity)).ToArray();
      return targetKeyValues.SequenceEqual(currentKeyValues);
    });
  }
}