using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Guard.Core.Services;

public class MaintenanceSectorService : IMaintenanceSectorService
{
  private readonly IReadRepository<MaintenanceSector> _readSectorRepository;
  private readonly IUnitOfWork _uow;
  private readonly ILogger<MaintenanceSectorService> _logger;

  public MaintenanceSectorService(
      IReadRepository<MaintenanceSector> readSectorRepository,
      IUnitOfWork unitOfWork,
      ILogger<MaintenanceSectorService> logger)
  {
    _readSectorRepository = readSectorRepository;
    _uow = unitOfWork;
    _logger = logger;
  }

  // ==================== Read (Изолированный IReadRepository) ====================

  public async Task<TResult> QuerySectorsAsync<TResult>(
      Func<IQueryable<MaintenanceSector>, Task<TResult>> query,
      CancellationToken ct = default)
  {
    return await _readSectorRepository.QueryAsync(query, ct);
  }

  public async Task<MaintenanceSector?> GetByIdAsync(Guid id, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос участка обслуживания по ID: {SectorId}", id);
    return await _readSectorRepository.GetByIdAsync(id, ct);
  }

  public async Task<IReadOnlyList<MaintenanceSector>> GetAllActiveAsync(CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос всех активных участков обслуживания");
    return await _readSectorRepository.QueryAsync(query =>
        query.Where(s => s.Status <= Status.Archived)
             .OrderBy(s => s.Name)
             .ToListAsync(ct),
        ct);
  }

  public async Task<IReadOnlyList<MaintenanceSector>> GetBySubdivisionIdAsync(Guid subdivisionId, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос участков обслуживания для подразделения ID: {SubdivisionId}", subdivisionId);
    return await _readSectorRepository.QueryAsync(query =>
        query.Where(s => s.SubdivisionId == subdivisionId && s.Status <= Status.Archived)
             .OrderBy(s => s.Name)
             .ToListAsync(ct),
        ct);
  }

  public async Task<IReadOnlyList<MaintenanceSector>> GetByResponsiblePersonalIdAsync(Guid responsiblePersonalId, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос участков обслуживания для ответственного сотрудника ID: {PersonalId}", responsiblePersonalId);
    return await _readSectorRepository.QueryAsync(query =>
        query.Where(s => s.ResponsiblePersonalId == responsiblePersonalId && s.Status <= Status.Archived)
             .OrderBy(s => s.Name)
             .ToListAsync(ct),
        ct);
  }

  public async Task<bool> IsNameUniqueAsync(string name, Guid? subdivisionId, Guid? excludeId = null, CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(name))
      return true;

    var normalizedName = name.Trim();

    return await _readSectorRepository.QueryAsync(query =>
        query.Where(s => s.Status <= Status.Archived) // Игнорируем удаленные записи
             .AllAsync(s =>
                (s.Name != normalizedName || s.SubdivisionId != subdivisionId) ||
                (excludeId.HasValue && s.Id == excludeId.Value),
                ct),
        ct);
  }

  // ==================== Write (IUnitOfWork & ChangeTracker) ====================

  public async Task<Guid> CreateAsync(MaintenanceSector sector, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(sector);

    return await _uow.ExecuteInTransactionAsync(async () =>
    {
      if (!string.IsNullOrWhiteSpace(sector.Name))
      {
        sector.Name = sector.Name.Trim();
      }

      if (!string.IsNullOrWhiteSpace(sector.Description))
      {
        sector.Description = sector.Description.Trim();
      }

      await _uow.BaseEntityRepository<MaintenanceSector>().AddAsync(sector, ct);
      await _uow.SaveChangesAsync(ct);

      _logger.LogInformation("Создан новый участок обслуживания '{SectorName}' (ID: {SectorId})",
          sector.Name, sector.Id);

      return sector.Id;
    }, ct);
  }

  public async Task UpdateAsync(MaintenanceSector sector, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(sector);

    await _uow.ExecuteInTransactionAsync(async () =>
    {
      // 1. Загружаем сущность из БД СО ВСЕМИ зависимыми коллекциями
      var existingSector = await _uow.BaseEntityRepository<MaintenanceSector>()
          .Query()
          .Include(s => s.SectorObjects)
          .Include(s => s.WeeklyPatterns)
          .FirstOrDefaultAsync(s => s.Id == sector.Id, ct);

      if (existingSector == null)
      {
        throw new KeyNotFoundException($"Участок обслуживания с ID {sector.Id} не найден.");
      }

      // 2. Обновляем основные поля
      existingSector.Name = sector.Name?.Trim() ?? string.Empty;
      existingSector.Description = sector.Description?.Trim();
      existingSector.SubdivisionId = sector.SubdivisionId;
      existingSector.ResponsiblePersonalId = sector.ResponsiblePersonalId;

      // 3. Синхронизируем коллекцию связей SectorObjects
      var targetObjectIds = sector.SectorObjects
          .Select(so => so.ProtectedObjectId)
          .ToHashSet();

      var itemsToRemove = existingSector.SectorObjects
          .Where(so => !targetObjectIds.Contains(so.ProtectedObjectId))
          .ToList();

      foreach (var itemToRemove in itemsToRemove)
      {
        existingSector.SectorObjects.Remove(itemToRemove);
      }

      var currentObjectIds = existingSector.SectorObjects
          .Select(so => so.ProtectedObjectId)
          .ToHashSet();

      var idsToAdd = targetObjectIds
          .Where(id => !currentObjectIds.Contains(id));

      foreach (var objectId in idsToAdd)
      {
        existingSector.SectorObjects.Add(new MaintenanceSectorObject
        {
          MaintenanceSectorId = existingSector.Id,
          ProtectedObjectId = objectId
        });
      }

      // 4. Синхронизируем недельный шаблон WeeklyPatterns
      if (sector.WeeklyPatterns != null && sector.WeeklyPatterns.Any())
      {
        foreach (var pattern in sector.WeeklyPatterns)
        {
          var existingPattern = existingSector.WeeklyPatterns
              .FirstOrDefault(p => p.DayOfWeek == pattern.DayOfWeek);

          if (existingPattern != null)
          {
            existingPattern.IsWorkDay = pattern.IsWorkDay;
            existingPattern.WorkStart = pattern.IsWorkDay ? pattern.WorkStart : null;
            existingPattern.WorkEnd = pattern.IsWorkDay ? pattern.WorkEnd : null;
          }
          else
          {
            existingSector.WeeklyPatterns.Add(new MaintenanceSectorWeeklyPattern
            {
              MaintenanceSectorId = existingSector.Id,
              DayOfWeek = pattern.DayOfWeek,
              IsWorkDay = pattern.IsWorkDay,
              WorkStart = pattern.IsWorkDay ? pattern.WorkStart : null,
              WorkEnd = pattern.IsWorkDay ? pattern.WorkEnd : null
            });
          }
        }
      }

      // 5. Фиксируем изменения
      await _uow.BaseEntityRepository<MaintenanceSector>().UpdateAsync(existingSector, ct);
      await _uow.SaveChangesAsync(ct);

      _logger.LogInformation("Обновлены данные, объекты и недельный шаблон участка обслуживания '{SectorName}' (ID: {SectorId})",
          existingSector.Name, existingSector.Id);
    }, ct);
  }

  // ==================== Status Management ====================

  public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
  {
    var sector = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<MaintenanceSector>().SoftDeleteAsync(sector, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Участок обслуживания '{SectorName}' (ID: {SectorId}) помечен как удаленный",
        sector.Name, id);
  }

  public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
  {
    var sector = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<MaintenanceSector>().ArchiveAsync(sector, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Участок обслуживания '{SectorName}' (ID: {SectorId}) отправлен в архив",
        sector.Name, id);
  }

  public async Task RestoreAsync(Guid id, CancellationToken ct = default)
  {
    var sector = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<MaintenanceSector>().RestoreAsync(sector, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Участок обслуживания '{SectorName}' (ID: {SectorId}) восстановлен",
        sector.Name, id);
  }

  // ==================== Private Helpers ====================

  private async Task<MaintenanceSector> GetRequiredForWriteAsync(Guid id, CancellationToken ct)
  {
    var sector = await _uow.BaseEntityRepository<MaintenanceSector>().GetByIdAsync(id, ct);

    if (sector == null)
    {
      _logger.LogWarning("Попытка выполнения операции над несуществующим участком обслуживания (ID: {SectorId})", id);
      throw new KeyNotFoundException($"Участок обслуживания с ID '{id}' не найден.");
    }

    return sector;
  }
}