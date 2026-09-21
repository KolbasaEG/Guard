using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Repositories;

/// <summary>
/// Универсальная реализация репозитория.
/// </summary>
public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
  protected readonly ApplicationDbContext Context;
  protected readonly DbSet<T> DbSet;

  public GenericRepository(ApplicationDbContext context)
  {
    Context = context;
    DbSet = context.Set<T>();
  }

  private static readonly HashSet<string> ReadOnlyAuditProperties = new()
  {
    nameof(BaseEntity.Id),
    nameof(BaseEntity.InsertedDate),
    nameof(BaseEntity.CreatedBy),
    nameof(BaseEntity.ModifiedBy),
    nameof(BaseEntity.LastModifiedDate)
   };

  // ==================== IQueryable ====================
  public IQueryable<T> Query() => DbSet.AsQueryable();


  // ==================== Read ====================
  public T? GetById(object id) => DbSet.Find(id);
  public async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
      => await DbSet.FindAsync(new[] { id }, ct);
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

    // 1. Быстрый поиск в ChangeTracker по Guid Id без рефлексии
    var trackedEntry = Context.ChangeTracker
        .Entries<T>()
        .FirstOrDefault(e => e.Entity.Id == entity.Id);

    // 2. Если объект не отслеживается — подтягиваем его из БД (Fetch-and-Update)
    if (trackedEntry == null)
    {
      var dbEntity = DbSet.Find(entity.Id)
          ?? throw new KeyNotFoundException($"Сущность {typeof(T).Name} с ID '{entity.Id}' не найдена.");

      trackedEntry = Context.Entry(dbEntity);
    }

    // 3. Единый безопасный цикл переноса полей для ОБОИХ случаев
    var incomingValues = Context.Entry(entity);

    foreach (var property in trackedEntry.Properties)
    {
      if (ReadOnlyAuditProperties.Contains(property.Metadata.Name))
        continue;

      property.CurrentValue = incomingValues.Property(property.Metadata.Name).CurrentValue;
    }
  }
  public async Task UpdateAsync(T entity, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(entity);

    var trackedEntry = Context.ChangeTracker
        .Entries<T>()
        .FirstOrDefault(e => e.Entity.Id == entity.Id);

    if (trackedEntry == null)
    {
      var dbEntity = await DbSet.FindAsync(new object[] { entity.Id }, ct)
          ?? throw new KeyNotFoundException($"Сущность {typeof(T).Name} с ID '{entity.Id}' не найдена.");

      trackedEntry = Context.Entry(dbEntity);
    }

    var incomingValues = Context.Entry(entity);

    foreach (var property in trackedEntry.Properties)
    {
      if (ReadOnlyAuditProperties.Contains(property.Metadata.Name))
        continue;

      property.CurrentValue = incomingValues.Property(property.Metadata.Name).CurrentValue;
    }
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
    foreach (var entity in entities)
    {
      await UpdateAsync(entity, ct);
    }
  }


  // ==================== Status Management ====================
  public void SoftDelete(T entity) => ChangeStatus(entity, Status.Deleted);
  public Task SoftDeleteAsync(T entity, CancellationToken ct = default) => ChangeStatusAsync(entity, Status.Deleted, ct);
  public void Archive(T entity) => ChangeStatus(entity, Status.Archived);
  public Task ArchiveAsync(T entity, CancellationToken ct = default) => ChangeStatusAsync(entity, Status.Archived, ct);
  public void Restore(T entity) => ChangeStatus(entity, Status.Modified);
  public Task RestoreAsync(T entity, CancellationToken ct = default) => ChangeStatusAsync(entity, Status.Modified, ct);
  public void Block(T entity)
  {
    var newStatus = entity.Status switch
    {
      Status.Archived => Status.ArchivedBlocked,
      _ => Status.Blocked
    };
    ChangeStatus(entity, newStatus);
  }
  public Task BlockAsync(T entity, CancellationToken ct = default)
  {
    var newStatus = entity.Status switch
    {
      Status.Archived => Status.ArchivedBlocked,
      _ => Status.Blocked
    };
    return ChangeStatusAsync(entity, newStatus, ct);
  }
  public void Unblock(T entity)
  {
    var newStatus = entity.Status switch
    {
      Status.ArchivedBlocked => Status.Archived,
      Status.Blocked => Status.Modified,
      _ => entity.Status
    };
    ChangeStatus(entity, newStatus);
  }
  public Task UnblockAsync(T entity, CancellationToken ct = default)
  {
    var newStatus = entity.Status switch
    {
      Status.ArchivedBlocked => Status.Archived,
      Status.Blocked => Status.Modified,
      _ => entity.Status
    };
    return ChangeStatusAsync(entity, newStatus, ct);
  }
  public void ChangeStatus(T entity, Status newStatus)
  {
    entity.Status = newStatus;
    Update(entity);
  }
  public async Task ChangeStatusAsync(T entity, Status newStatus, CancellationToken ct = default)
  {
    entity.Status = newStatus;
    await UpdateAsync(entity, ct); // Не заблокирует поток, если потребуется FindAsync в БД
  }


  // ==================== Delete (физическое) ====================
  public void Delete(T entity) => DbSet.Remove(entity);
  public async Task DeleteAsync(T entity, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(entity);

    var trackedEntry = Context.ChangeTracker
        .Entries<T>()
        .FirstOrDefault(e => e.Entity.Id == entity.Id);

    if (trackedEntry == null)
    {
      // Безопасный асинхронный поиск перед удалением неотслеживаемого объекта
      var dbEntity = await DbSet.FindAsync(new object[] { entity.Id }, ct)
          ?? throw new KeyNotFoundException($"Сущность {typeof(T).Name} с ID '{entity.Id}' не найдена.");

      DbSet.Remove(dbEntity);
    }
    else
    {
      DbSet.Remove(trackedEntry.Entity);
    }
  }
  public void DeleteRange(IEnumerable<T> entities) => DbSet.RemoveRange(entities);
  public async Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
  {
    foreach (var entity in entities)
    {
      await DeleteAsync(entity, ct);
    }
  }


  // ==================== Дополнительные методы ====================
  public bool Exists(object id) => id is Guid guidId && DbSet.Any(e => e.Id == guidId);
  public async Task<bool> ExistsAsync(object id, CancellationToken ct = default)
      => id is Guid guidId && await DbSet.AnyAsync(e => e.Id == guidId, ct);
  public int Count() => DbSet.Count();
  public async Task<int> CountAsync(CancellationToken ct = default)
      => await DbSet.CountAsync(ct);

}