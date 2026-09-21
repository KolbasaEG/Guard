using Guard.Core.Contexts;
using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

/// <summary>
/// Сервис управления классификаторами для администратора.
/// </summary>
public class ClassifierService : IClassifierService
{
  private readonly ApplicationDbContext _context;
  protected readonly DbSet<Classifier> DbSet;
  public ClassifierService(ApplicationDbContext context)
  {
    _context = context;
    DbSet = context.Set<Classifier>();
  }

  // ==================== IQueryable ====================
  public IQueryable<Classifier> Query() => DbSet.AsQueryable();


  // ==================== Read ====================
  public Classifier? GetById(object id) => DbSet.Find(id);
  public async Task<Classifier?> GetByIdAsync(object id, CancellationToken ct = default)
      => await DbSet.FindAsync(new[] { id }, ct);
  public IReadOnlyList<Classifier> GetAll()
      => DbSet.AsNoTracking().ToList();
  public async Task<IReadOnlyList<Classifier>> GetAllAsync(CancellationToken ct = default)
      => await DbSet.AsNoTracking().ToListAsync(ct);


  // ==================== Create ====================
  public void Add(Classifier entity) 
  {
    DbSet.Add(entity);
    _context.SaveChanges();
  }
  public async Task<int> AddAsync(Classifier entity)
  {
    var z = await DbSet.AddAsync(entity);
    var result = await _context.SaveChangesAsync();
    return result;
  }

  // ==================== Update ====================
  public void Update(Classifier entity)
  {
    var trackedEntry = _context.ChangeTracker
        .Entries<Classifier>()
        .FirstOrDefault(e => HasSameKey(e.Entity, entity));

    if (trackedEntry != null)
    {
      trackedEntry.CurrentValues.SetValues(entity);
      trackedEntry.State = EntityState.Modified;
    }
    else
    {
      DbSet.Update(entity);
    }
    _context.SaveChanges();
  }


  // ==================== Status Management ====================
  public void SoftDelete(Classifier entity)
  {
    entity.IsActive = false;
    Update(entity);
  }
  
  public void Restore(Classifier entity)
  {
    entity.IsActive = true;
    Update(entity);
  }
  public void ChangeStatus(Classifier entity, bool newStatus)
  {
    entity.IsActive = newStatus;
    Update(entity);
  }


  // ==================== Delete (физическое) ====================
  public void Delete(Classifier entity)
  {
    DbSet.Remove(entity);
    _context.SaveChanges();
  }


  // ==================== Дополнительные методы ====================
  public bool Exists(object id) => DbSet.Find(id) is not null;
  public async Task<bool> ExistsAsync(object id, CancellationToken ct = default)
  {
    var entity = await DbSet.FindAsync(new[] { id }, ct);
    return entity is not null;
  }
  public int Count() => DbSet.Count();
  public async Task<int> CountAsync(CancellationToken ct = default)
      => await DbSet.CountAsync(ct);


  // ==================== Private Helpers ====================
  private bool HasSameKey(Classifier left, Classifier right)
  {
    var keyProperties = _context.Model
        .FindEntityType(typeof(Classifier))
        ?.FindPrimaryKey()
        ?.Properties;

    if (keyProperties == null || !keyProperties.Any())
      return false;

    foreach (var property in keyProperties)
    {
      var leftValue = _context.Entry(left).Property(property.Name).CurrentValue;
      var rightValue = _context.Entry(right).Property(property.Name).CurrentValue;

      if (!Equals(leftValue, rightValue))
        return false;
    }
    return true;
  }
}