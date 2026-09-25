using Guard.Core.Entities;
using Guard.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public class OrganTypeService : IOrganTypeService
{
  private readonly IReadRepository<OrganType> _readOrganTypeRepository;
  private readonly IUnitOfWork _uow;
  private readonly ILogger<OrganTypeService> _logger;

  public OrganTypeService(
      IReadRepository<OrganType> readOrganTypeRepository,
      IUnitOfWork unitOfWork,
      ILogger<OrganTypeService> logger)
  {
    _readOrganTypeRepository = readOrganTypeRepository;
    _uow = unitOfWork;
    _logger = logger;
  }

  // ==================== Read (Изолированный IReadRepository) ====================

  public async Task<TResult> QueryOrganTypesAsync<TResult>(
      Func<IQueryable<OrganType>, Task<TResult>> query,
      CancellationToken ct = default)
  {
    return await _readOrganTypeRepository.QueryAsync(query, ct);
  }

  public async Task<OrganType?> GetByIdAsync(int id, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос типа органа по ID: {OrganTypeId}", id);
    return await _readOrganTypeRepository.GetByIdAsync(id, ct);
  }

  public async Task<OrganType?> GetByCodeAsync(int code, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос типа органа по коду: {Code}", code);
    return await _readOrganTypeRepository.QueryAsync(query =>
        query.FirstOrDefaultAsync(ot => ot.Code == code, ct),
        ct);
  }

  public async Task<IReadOnlyList<OrganType>> GetAllAsync(CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос всех типов органов");
    return await _readOrganTypeRepository.QueryAsync(query =>
        query.OrderBy(ot => ot.Code)
             .ToListAsync(ct),
        ct);
  }

  public async Task<bool> IsCodeUniqueAsync(int code, int? excludeId = null, CancellationToken ct = default)
  {
    return await _readOrganTypeRepository.QueryAsync(query =>
        query.AllAsync(ot => ot.Code != code || (excludeId.HasValue && ot.Id == excludeId.Value), ct),
        ct);
  }

  // ==================== Write (IUnitOfWork & BasicRepository) ====================

  public async Task<int> CreateAsync(OrganType organType, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(organType);

    return await _uow.ExecuteInTransactionAsync(async () =>
    {
      if (organType.ClassifierType == 0)
      {
        organType.ClassifierType = 906;
      }

      if (!string.IsNullOrWhiteSpace(organType.Name))
      {
        organType.Name = organType.Name.Trim();
      }

      await _uow.BasicRepository<OrganType>().AddAsync(organType, ct);
      await _uow.SaveChangesAsync(ct);

      _logger.LogInformation("Создан новый тип органа '{Name}' (Code: {Code}, ID: {OrganTypeId})",
          organType.Name, organType.Code, organType.Id);

      return organType.Id;
    }, ct);
  }

  public async Task UpdateAsync(OrganType organType, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(organType);

    if (!string.IsNullOrWhiteSpace(organType.Name))
    {
      organType.Name = organType.Name.Trim();
    }

    await _uow.BasicRepository<OrganType>().UpdateAsync(organType, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Обновлен тип органа '{Name}' (ID: {OrganTypeId})",
        organType.Name, organType.Id);
  }

  public async Task DeleteAsync(int id, CancellationToken ct = default)
  {
    var organType = await GetRequiredForWriteAsync(id, ct);

    await _uow.BasicRepository<OrganType>().DeleteAsync(organType, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Тип органа '{Name}' (ID: {OrganTypeId}) был удален",
        organType.Name, id);
  }

  // ==================== Private Helpers ====================

  private async Task<OrganType> GetRequiredForWriteAsync(int id, CancellationToken ct)
  {
    var organType = await _uow.BasicRepository<OrganType>().GetByIdAsync(new object[] { id }, ct);

    if (organType == null)
    {
      _logger.LogWarning("Попытка выполнения операции над несуществующим типом органа (ID: {OrganTypeId})", id);
      throw new KeyNotFoundException($"Тип органа с ID '{id}' не найден.");
    }

    return organType;
  }
}