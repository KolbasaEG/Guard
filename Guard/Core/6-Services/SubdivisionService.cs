using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public class SubdivisionService : ISubdivisionService
{
  private readonly IReadRepository<Subdivision> _readSubdivisionRepository;
  private readonly IUnitOfWork _uow;
  private readonly ILogger<SubdivisionService> _logger;

  public SubdivisionService(
      IReadRepository<Subdivision> readRepository,
      IUnitOfWork unitOfWork,
      ILogger<SubdivisionService> logger)
  {
    _readSubdivisionRepository = readRepository;
    _uow = unitOfWork;
    _logger = logger;
  }

  // ==================== Read (Изолированный IReadRepository) ====================

  public async Task<TResult> QuerySubdivisionsAsync<TResult>(
        Func<IQueryable<Subdivision>, Task<TResult>> query,
        CancellationToken ct = default)
  {
    return await _readSubdivisionRepository.QueryAsync(query, ct);
  }

  public async Task<Subdivision?> GetByIdAsync(Guid id, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос подразделения по ID: {SubdivisionId}", id);
    return await _uow.Repository<Subdivision>().GetByIdAsync(id, ct);
  }

  public async Task<IReadOnlyList<Subdivision>> GetAllActiveAsync(CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос всех активных подразделений");
    return await _readSubdivisionRepository.QueryAsync(query =>
        query.Where(s => s.Status <= Status.Archived)
             .OrderBy(s => s.Name)
             .ToListAsync(ct),
        ct);
  }

  public async Task<IReadOnlyList<Subdivision>> GetByParentIdAsync(Guid? parentId, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос дочерних подразделений для ParentId: {ParentId}", parentId);
    return await _readSubdivisionRepository.QueryAsync(query =>
        query.Where(s => s.ParentId == parentId && s.Status <= Status.Archived)
             .ToListAsync(ct),
        ct);
  }

  // ==================== Write (IUnitOfWork & ChangeTracker) ====================

  public async Task<Guid> CreateAsync(Subdivision subdivision, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(subdivision);

    return await _uow.ExecuteInTransactionAsync(async () =>
    {
      subdivision.Status = Status.Inserted;

      // 1. Определение пути родителя
      string parentPath = "/";
      if (subdivision.ParentId.HasValue)
      {
        var parent = await _uow.Repository<Subdivision>().GetByIdAsync(subdivision.ParentId.Value, ct);
        if (parent == null)
          throw new KeyNotFoundException($"Родительское подразделение с ID '{subdivision.ParentId}' не найдено.");

        parentPath = parent.Path;
      }

      // 2. Добавление записи для получения сгенерированного СУБД SubdivisionId
      await _uow.Repository<Subdivision>().AddAsync(subdivision, ct);
      await _uow.SaveChangesAsync(ct);

      // 3. Формирование Path на основе полученного SubdivisionId (например: "/1/4/12/")
      subdivision.Path = $"{parentPath}{subdivision.SubdivisionId}/";
      await _uow.Repository<Subdivision>().UpdateAsync(subdivision, ct);
      await _uow.SaveChangesAsync(ct);

      _logger.LogInformation("Создано новое подразделение '{SubdivisionName}' с Path: '{Path}' (ID: {SubdivisionId})",
          subdivision.Name, subdivision.Path, subdivision.Id);

      return subdivision.Id;
    }, ct);
  }

  public async Task UpdateAsync(Subdivision subdivision, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(subdivision);

    await _uow.Repository<Subdivision>().UpdateAsync(subdivision, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Обновлены данные подразделения '{SubdivisionName}' (ID: {SubdivisionId})",
        subdivision.Name, subdivision.Id);
  }

  public async Task MoveAsync(Guid id, Guid? newParentId, CancellationToken ct = default)
  {
    await _uow.ExecuteInTransactionAsync(async () =>
    {
      var target = await GetRequiredForWriteAsync(id, ct);

      // Нет изменений родителя — выходим
      if (target.ParentId == newParentId)
        return;

      string oldPath = target.Path;
      string newParentPath = "/";

      if (newParentId.HasValue)
      {
        // 1. Проверка попытки назначить родителем самого себя
        if (newParentId.Value == target.Id)
          throw new InvalidOperationException("Нельзя переместить подразделение в самого себя.");

        var newParent = await _uow.Repository<Subdivision>().GetByIdAsync(newParentId.Value, ct);
        if (newParent == null)
          throw new KeyNotFoundException($"Новое родительское подразделение с ID '{newParentId}' не найдено.");

        // 2. Проверка циклической зависимости (нельзя переместить родителя в его потомка)
        if (!string.IsNullOrEmpty(oldPath) && newParent.Path.StartsWith(oldPath, StringComparison.OrdinalIgnoreCase))
          throw new InvalidOperationException("Нельзя переместить подразделение в одного из его подчиненных узлов.");

        newParentPath = newParent.Path;
      }

      // Новый базовый путь целевого подразделения
      string newPath = $"{newParentPath}{target.SubdivisionId}/";

      // 3. Выборка целевого узла и ВСЕХ его потомков через Prefix Match (StartsWith)
      var repo = _uow.Repository<Subdivision>();
      var affectedSubdivisions = await repo.Query()
          .Where(s => s.Path.StartsWith(oldPath))
          .ToListAsync(ct);

      _logger.LogInformation("Запуск перемещения подразделения '{Name}'. Старый Path: '{OldPath}', Новый Path: '{NewPath}'. Затронуто потомков: {Count}",
          target.Name, oldPath, newPath, affectedSubdivisions.Count);

      // 4. Пересчет Path для узла и каждого потомка
      foreach (var item in affectedSubdivisions)
      {
        // Заменяем префикс oldPath на newPath
        item.Path = newPath + item.Path[oldPath.Length..];

        if (item.Id == id)
        {
          item.ParentId = newParentId;
        }

        await repo.UpdateAsync(item, ct);
      }

      await _uow.SaveChangesAsync(ct);
    }, ct);
  }

  // ==================== Status Management ====================

  public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
  {
    var subdivision = await GetRequiredForWriteAsync(id, ct);

    await _uow.Repository<Subdivision>().SoftDeleteAsync(subdivision, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Подразделение '{SubdivisionName}' (ID: {SubdivisionId}) помечено как удаленное",
        subdivision.Name, id);
  }

  public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
  {
    var subdivision = await GetRequiredForWriteAsync(id, ct);

    await _uow.Repository<Subdivision>().ArchiveAsync(subdivision, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Подразделение '{SubdivisionName}' (ID: {SubdivisionId}) отправлено в архив",
        subdivision.Name, id);
  }

  public async Task BlockAsync(Guid id, CancellationToken ct = default)
  {
    var subdivision = await GetRequiredForWriteAsync(id, ct);

    await _uow.Repository<Subdivision>().BlockAsync(subdivision, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Подразделение '{SubdivisionName}' (ID: {SubdivisionId}) заблокировано",
        subdivision.Name, id);
  }

  public async Task UnblockAsync(Guid id, CancellationToken ct = default)
  {
    var subdivision = await GetRequiredForWriteAsync(id, ct);

    await _uow.Repository<Subdivision>().UnblockAsync(subdivision, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Подразделение '{SubdivisionName}' (ID: {SubdivisionId}) разблокировано",
        subdivision.Name, id);
  }

  public async Task RestoreAsync(Guid id, CancellationToken ct = default)
  {
    var subdivision = await GetRequiredForWriteAsync(id, ct);

    await _uow.Repository<Subdivision>().RestoreAsync(subdivision, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Подразделение '{SubdivisionName}' (ID: {SubdivisionId}) восстановлено",
        subdivision.Name, id);
  }

  // ==================== Private Helpers ====================

  private async Task<Subdivision> GetRequiredForWriteAsync(Guid id, CancellationToken ct)
  {
    var subdivision = await _uow.Repository<Subdivision>().GetByIdAsync(id, ct);

    if (subdivision == null)
    {
      _logger.LogWarning("Попытка выполнения операции над несуществующим подразделением (ID: {SubdivisionId})", id);
      throw new KeyNotFoundException($"Подразделение с ID '{id}' не найдено.");
    }

    return subdivision;
  }
}