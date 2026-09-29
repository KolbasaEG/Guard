using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Guard.Core.Services;

public class ProtectedObjectService : IProtectedObjectService
{
  private readonly IReadRepository<ProtectedObject> _readRepository;
  private readonly IUnitOfWork _uow;
  private readonly ILogger<ProtectedObjectService> _logger;

  public ProtectedObjectService(
      IReadRepository<ProtectedObject> readRepository,
      IUnitOfWork unitOfWork,
      ILogger<ProtectedObjectService> logger)
  {
    _readRepository = readRepository;
    _uow = unitOfWork;
    _logger = logger;
  }

  // ==================== Read (Изолированный IReadRepository) ====================

  public async Task<TResult> QueryObjectsAsync<TResult>(
      Func<IQueryable<ProtectedObject>, Task<TResult>> query,
      CancellationToken ct = default)
  {
    return await _readRepository.QueryAsync(query, ct);
  }

  public async Task<ProtectedObject?> GetByIdAsync(Guid id, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос охраняемого объекта по ID: {ObjectId}", id);
    return await _readRepository.GetByIdAsync(id, ct);
  }

  public async Task<IReadOnlyList<ProtectedObject>> GetAllActiveAsync(CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос всех активных охраняемых объектов");
    return await _readRepository.QueryAsync(query =>
        query.Where(o => o.Status <= Status.Archived)
             .OrderBy(o => o.Name)
             .ToListAsync(ct),
        ct);
  }

  public async Task<IReadOnlyList<ProtectedObject>> GetBySubdivisionIdAsync(Guid subdivisionId, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос охраняемых объектов для подразделения ID: {SubdivisionId}", subdivisionId);
    return await _readRepository.QueryAsync(query =>
        query.Where(o => o.SubdivisionId == subdivisionId && o.Status <= Status.Archived)
             .OrderBy(o => o.Name)
             .ToListAsync(ct),
        ct);
  }

  public async Task<bool> IsNameUniqueAsync(string name, Guid? subdivisionId, Guid? excludeId = null, CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(name))
      return true;

    var normalizedName = name.Trim();

    return await _readRepository.QueryAsync(query =>
        query.AllAsync(o =>
            (o.Name != normalizedName || o.SubdivisionId != subdivisionId) ||
            (excludeId.HasValue && o.Id == excludeId.Value),
            ct),
        ct);
  }

  // ==================== Write (IUnitOfWork & ChangeTracker) ====================

  public async Task<Guid> CreateAsync(ProtectedObject protectedObject, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(protectedObject);

    return await _uow.ExecuteInTransactionAsync(async () =>
    {
      protectedObject.Status = Status.Inserted;

      NormalizeStringProperties(protectedObject);

      await _uow.BaseEntityRepository<ProtectedObject>().AddAsync(protectedObject, ct);
      await _uow.SaveChangesAsync(ct);

      _logger.LogInformation("Создан новый охраняемый объект '{ObjectName}' (ID: {ObjectId})",
          protectedObject.Name, protectedObject.Id);

      return protectedObject.Id;
    }, ct);
  }

  public async Task UpdateAsync(ProtectedObject protectedObject, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(protectedObject);

    NormalizeStringProperties(protectedObject);

    await _uow.BaseEntityRepository<ProtectedObject>().UpdateAsync(protectedObject, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Обновлены данные охраняемого объекта '{ObjectName}' (ID: {ObjectId})",
        protectedObject.Name, protectedObject.Id);
  }

  // ==================== Status Management ====================

  public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
  {
    var obj = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<ProtectedObject>().SoftDeleteAsync(obj, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Охраняемый объект '{ObjectName}' (ID: {ObjectId}) помечен как удаленный",
        obj.Name, id);
  }

  public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
  {
    var obj = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<ProtectedObject>().ArchiveAsync(obj, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Охраняемый объект '{ObjectName}' (ID: {ObjectId}) отправлен в архив",
        obj.Name, id);
  }

  public async Task RestoreAsync(Guid id, CancellationToken ct = default)
  {
    var obj = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<ProtectedObject>().RestoreAsync(obj, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Охраняемый объект '{ObjectName}' (ID: {ObjectId}) восстановлен",
        obj.Name, id);
  }

  // ==================== Private Helpers ====================

  private async Task<ProtectedObject> GetRequiredForWriteAsync(Guid id, CancellationToken ct)
  {
    var obj = await _uow.BaseEntityRepository<ProtectedObject>().GetByIdAsync(id, ct);

    if (obj == null)
    {
      _logger.LogWarning("Попытка выполнения операции над несуществующим охраняемым объектом (ID: {ObjectId})", id);
      throw new KeyNotFoundException($"Охраняемый объект с ID '{id}' не найден.");
    }

    return obj;
  }

  private static void NormalizeStringProperties(ProtectedObject obj)
  {
    if (!string.IsNullOrWhiteSpace(obj.Name)) obj.Name = obj.Name.Trim();
    if (!string.IsNullOrWhiteSpace(obj.FullName)) obj.FullName = obj.FullName.Trim();
    if (!string.IsNullOrWhiteSpace(obj.LegalAddress)) obj.LegalAddress = obj.LegalAddress.Trim();
    if (!string.IsNullOrWhiteSpace(obj.Phone)) obj.Phone = obj.Phone.Trim();
    if (!string.IsNullOrWhiteSpace(obj.Note)) obj.Note = obj.Note.Trim();
    if (!string.IsNullOrWhiteSpace(obj.ObjectId)) obj.ObjectId = obj.ObjectId.Trim();
  }
}