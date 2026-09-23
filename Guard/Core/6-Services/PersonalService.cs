using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public class PersonalService : IPersonalService
{
  private readonly IReadRepository<Personal> _readPersonalRepository;
  private readonly IReadRepository<Subdivision> _readSubdivisionRepository;
  private readonly IUnitOfWork _uow;
  private readonly ILogger<PersonalService> _logger;

  public PersonalService(
      IReadRepository<Personal> readPersonalRepository,
      IReadRepository<Subdivision> readSubdivisionRepository, IUnitOfWork unitOfWork,
      ILogger<PersonalService> logger)
  {
    _readPersonalRepository = readPersonalRepository;
    _readSubdivisionRepository = readSubdivisionRepository;

    _uow = unitOfWork;
    _logger = logger;
  }
  public async Task<IReadOnlyList<Subdivision>> GetAllActiveSubdivisionsAsync(CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос всех активных подразделений");
    return await _readSubdivisionRepository.QueryAsync(query =>
        query.Where(s => s.Status <= Status.Archived)
             .OrderBy(s => s.Name)
             .ToListAsync(ct),
        ct);
  }

  // ==================== Read (Изолированный IReadRepository) ====================

  public async Task<TResult> QueryPersonalsAsync<TResult>(
      Func<IQueryable<Personal>, Task<TResult>> query,
      CancellationToken ct = default)
  {
    return await _readPersonalRepository.QueryAsync(query, ct);
  }

  public async Task<Personal?> GetByIdAsync(Guid id, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос персонала по ID: {PersonalId}", id);
    return await _uow.BaseEntityRepository<Personal>().GetByIdAsync(id, ct);
  }

  public async Task<IReadOnlyList<Personal>> GetAllActiveAsync(CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос всех активных сотрудников");
    return await _readPersonalRepository.QueryAsync(query =>
        query.Where(p => p.Status != Status.Deleted && p.Status != Status.Archived)
             .OrderBy(p => p.LastName)
             .ThenBy(p => p.FirstName)
             .ToListAsync(ct),
        ct);
  }

  public async Task<IReadOnlyList<Personal>> GetBySubdivisionIdAsync(Guid subdivisionId, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос персонала для подразделения с ID: {SubdivisionId}", subdivisionId);
    return await _readPersonalRepository.QueryAsync(query =>
        query.Where(p => p.SubdivisionId == subdivisionId && p.Status != Status.Deleted)
             .OrderBy(p => p.LastName)
             .ThenBy(p => p.FirstName)
             .ToListAsync(ct),
        ct);
  }

  public async Task<IReadOnlyList<Personal>> GetBySubdivisionPathAsync(
      string targetPath,
      SubdivisionHierarchyMode mode = SubdivisionHierarchyMode.IncludeChildren,
      CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос персонала для пути подразделения '{TargetPath}' с режимом {HierarchyMode}", targetPath, mode);
    return await _readPersonalRepository.QueryAsync(query =>
        query.Where(p => p.Status != Status.Deleted && p.Status != Status.Archived)
             .FilterBySubdivision(targetPath, mode)
             .OrderBy(p => p.LastName)
             .ThenBy(p => p.FirstName)
             .ToListAsync(ct),
        ct);
  }

  // ==================== Write (IUnitOfWork & ChangeTracker) ====================

  public async Task<Guid> CreateAsync(Personal Personal, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(Personal);

    Personal.Status = Status.Inserted;

    await _uow.BaseEntityRepository<Personal>().AddAsync(Personal, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Создана новая запись сотрудника '{LastName} {FirstName}' с ID: {PersonalId}",
        Personal.LastName, Personal.FirstName, Personal.Id);

    return Personal.Id;
  }

  public async Task UpdateAsync(Personal Personal, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(Personal);

    Personal.UpdatedAt = DateTime.UtcNow;

    await _uow.BaseEntityRepository<Personal>().UpdateAsync(Personal, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Обновлены данные сотрудника '{LastName} {FirstName}' (ID: {PersonalId})",
        Personal.LastName, Personal.FirstName, Personal.Id);
  }

  // ==================== Status Management ====================

  public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
  {
    var Personal = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<Personal>().SoftDeleteAsync(Personal, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Сотрудник '{LastName} {FirstName}' (ID: {PersonalId}) помечен как удаленный",
        Personal.LastName, Personal.FirstName, id);
  }

  public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
  {
    var Personal = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<Personal>().ArchiveAsync(Personal, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Сотрудник '{LastName} {FirstName}' (ID: {PersonalId}) отправлен в архив",
        Personal.LastName, Personal.FirstName, id);
  }

  public async Task BlockAsync(Guid id, CancellationToken ct = default)
  {
    var Personal = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<Personal>().BlockAsync(Personal, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Сотрудник '{LastName} {FirstName}' (ID: {PersonalId}) заблокирован",
        Personal.LastName, Personal.FirstName, id);
  }

  public async Task UnblockAsync(Guid id, CancellationToken ct = default)
  {
    var Personal = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<Personal>().UnblockAsync(Personal, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Сотрудник '{LastName} {FirstName}' (ID: {PersonalId}) разблокирован",
        Personal.LastName, Personal.FirstName, id);
  }

  public async Task RestoreAsync(Guid id, CancellationToken ct = default)
  {
    var Personal = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<Personal>().RestoreAsync(Personal, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Сотрудник '{LastName} {FirstName}' (ID: {PersonalId}) восстановлен",
        Personal.LastName, Personal.FirstName, id);
  }

  // ==================== Private Helpers ====================

  private async Task<Personal> GetRequiredForWriteAsync(Guid id, CancellationToken ct)
  {
    var Personal = await _uow.BaseEntityRepository<Personal>().GetByIdAsync(id, ct);

    if (Personal == null)
    {
      _logger.LogWarning("Попытка выполнения операции над несуществующей записью персонала (ID: {PersonalId})", id);
      throw new KeyNotFoundException($"Сотрудник с ID '{id}' не найден.");
    }

    return Personal;
  }
}