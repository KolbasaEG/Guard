using Guard.Domain.Entities;
using Guard.Domain.Specifications;
using Guard.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Guard.Infrastructure.Services
{
  /// <summary>
  /// Реализация репозитория для работы с подразделениями.
  /// </summary>
  public class SubdivisionRepository : ISubdivisionRepository
  {
    private readonly GuardDbContext _context;

    /// <summary>
    /// Конструктор репозитория.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    public SubdivisionRepository(GuardDbContext context)
    {
      _context = context;
    }

    /// <inheritdoc />
    public async Task<Subdivision> GetByIdAsync(Guid id)
    {
      return await _context.Subdivisions.FindAsync(id);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Subdivision>> GetAllAsync()
    {
      return await _context.Subdivisions.ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Subdivision> CreateAsync(Subdivision subdivision)
    {
      _context.Subdivisions.Add(subdivision);
      await _context.SaveChangesAsync();
      return subdivision;
    }

    /// <inheritdoc />
    public async Task<Subdivision> UpdateAsync(Subdivision subdivision)
    {
      _context.Subdivisions.Update(subdivision);
      await _context.SaveChangesAsync();
      return subdivision;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Subdivision subdivision)
    {
      _context.Subdivisions.Remove(subdivision);
      await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string name)
    {
      return await _context.Subdivisions.AnyAsync(s => s.Наименование == name);
    }

    /// <inheritdoc />
    public async Task<Subdivision> FindAsync(ISpecification<Subdivision> specification)
    {
      var subdivisions = await GetAllAsync();
      return subdivisions.FirstOrDefault(subdivision => specification.IsSatisfiedBy(subdivision));
    }
  }
}