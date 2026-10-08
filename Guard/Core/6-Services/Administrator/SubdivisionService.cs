using Guard.Core.Services.DTOs;
using Guard.Core.Identity;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public class SubdivisionService : ISubdivisionService
{
  private readonly IReadRepository<Subdivision> _readSubdivisionRepository;
  private readonly IReadRepository<OrganType> _readOrganTypeRepository;
  private readonly IReadRepository<Classifier> _readClassifierRepository;
  private readonly IUnitOfWorkFactory _writes;
  private readonly ILogger<SubdivisionService> _logger;
  private readonly IDbContextFactory<ApplicationDbContext> _factory;
  private readonly IPermissionService _permissions;
  private readonly IDataAccessScopeService _scopes;

  public SubdivisionService(
      IReadRepository<Subdivision> readSubdivisionRepository,
      IReadRepository<OrganType> readOrganTypeRepository,
      IReadRepository<Classifier> readClassifierRepository,
      IUnitOfWorkFactory unitOfWork,
      ILogger<SubdivisionService> logger, IPermissionService permissions, IDataAccessScopeService scopes, IDbContextFactory<ApplicationDbContext> factory)
  {
    _readSubdivisionRepository = readSubdivisionRepository;
    _readOrganTypeRepository = readOrganTypeRepository;
    _readClassifierRepository = readClassifierRepository;
    _writes = unitOfWork;
    _logger = logger;
    _factory = factory;
    _permissions = permissions;
    _scopes = scopes;
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
    return await _readSubdivisionRepository.QueryAsync(q => q.Include(s => s.OrganType).Include(s => s.StatusClassifier).SingleOrDefaultAsync(s => s.Id == id, ct), ct);
  }

  public async Task<IReadOnlyList<Subdivision>> GetAllActiveAsync(CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос всех активных подразделений");
    return await _readSubdivisionRepository.QueryAsync(query =>
        query.Where(s => (s.Status == Status.Inserted || s.Status == Status.Modified))
             .OrderBy(s => s.Name)
             .ToListAsync(ct),
        ct);
  }

  public async Task<IReadOnlyList<Subdivision>> GetByParentIdAsync(Guid? parentId, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос дочерних подразделений для ParentId: {ParentId}", parentId);
    return await _readSubdivisionRepository.QueryAsync(query =>
        query.Where(s => s.ParentId == parentId && (s.Status == Status.Inserted || s.Status == Status.Modified))
             .ToListAsync(ct),
        ct);
  }

  // ==================== Write (IUnitOfWork & ChangeTracker) ====================

  public async Task<Guid> CreateAsync(Subdivision subdivision, CancellationToken ct = default)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Subdivisions.Write, ct);
    ArgumentNullException.ThrowIfNull(subdivision);
    var clean = new Subdivision { Id = subdivision.Id, ParentId = subdivision.ParentId, SubdivisionId = subdivision.SubdivisionId, ParentSubdivisionId = subdivision.ParentSubdivisionId };
    SubdivisionFieldsDto.From(subdivision).ApplyTo(clean); subdivision = clean;
    EntityInputValidation.Validate(subdivision);

    return await _uow.ExecuteInTransactionAsync(async () =>
    {
      if (!(await _scopes.GetAsync(ct)).Allows(subdivision.ParentId)) throw new UnauthorizedAccessException("Родительское подразделение недоступно.");
      subdivision.Status = Status.Inserted;
      await EntityReferences.SubdivisionAsync(_uow, subdivision, null, ct);

      // 1. Определение пути родителя
      string parentPath = "/";
      if (subdivision.ParentId.HasValue)
      {
        var parent = await _uow.BaseEntityRepository<Subdivision>().GetByIdAsync(subdivision.ParentId.Value, ct);
        if (parent == null)
          throw new KeyNotFoundException($"Родительское подразделение с ID '{subdivision.ParentId}' не найдено.");

        if (parent.Status is not (Status.Inserted or Status.Modified)) throw new ArgumentException("Выберите активное родительское подразделение.");
        parentPath = parent.Path;
      }

      // 2. Если ID НЕ передан (автогенерация) — выравниваем счетчик ДО сохранения, 
      // чтобы защититься от старых рассинхронов
      if (!subdivision.SubdivisionId.HasValue)
      {
        await _uow.ExecuteSqlRawAsync(
            """
          SELECT setval(
              pg_get_serial_sequence('"Subdivisions"', 'SubdivisionId'), 
              COALESCE((SELECT MAX("SubdivisionId") FROM "Subdivisions"), 0)
          );
          """,
            ct);
      }

      // 3. Добавление записи (при null PostgreSQL возьмет следующий корректный значение из sequence)
      await _uow.BaseEntityRepository<Subdivision>().AddAsync(subdivision, ct);
      await _uow.SaveChangesAsync(ct);

      // 4. Если ID БЫЛ передан вручную — выравниваем счетчик ПОСЛЕ сохранения, 
      // чтобы sequence перешагнул через вставленное вручную значение
      if (subdivision.SubdivisionId.HasValue)
      {
        await _uow.ExecuteSqlRawAsync(
            """
          SELECT setval(
              pg_get_serial_sequence('"Subdivisions"', 'SubdivisionId'), 
              COALESCE((SELECT MAX("SubdivisionId") FROM "Subdivisions"), 0)
          );
          """,
            ct);
      }

      // 5. Формирование Path на основе полученного SubdivisionId
      subdivision.Path = $"{parentPath}{subdivision.SubdivisionId}/";
      await _uow.BaseEntityRepository<Subdivision>().UpdateAsync(subdivision, ct);
      await _uow.SaveChangesAsync(ct);

      _logger.LogInformation("Создано новое подразделение '{SubdivisionName}' с Path: '{Path}' (ID: {SubdivisionId})",
          subdivision.Name, subdivision.Path, subdivision.Id);

      return subdivision.Id;
    }, ct);
  }
  public async Task UpdateAsync(Subdivision subdivision, CancellationToken ct = default)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Subdivisions.Write, ct);
    ArgumentNullException.ThrowIfNull(subdivision);
    EntityInputValidation.Validate(subdivision);

    var existing = await GetRequiredForWriteAsync(_uow, subdivision.Id, ct) ?? throw new KeyNotFoundException("Подразделение недоступно.");
    if (existing.Status is not (Status.Inserted or Status.Modified)) throw new EntityRuleException("Подразделение неактивно или заблокировано.");
    if (existing.ParentId != subdivision.ParentId || existing.Path != subdivision.Path || existing.SubdivisionId != subdivision.SubdivisionId)
      throw new EntityRuleException("Для изменения родителя используйте перемещение подразделения.");
    subdivision.Status = existing.Status;
    EntityInputValidation.CheckVersion(existing.Version, subdivision.Version);
    await EntityReferences.SubdivisionAsync(_uow, subdivision, existing, ct);
    SubdivisionFieldsDto.From(subdivision).ApplyTo(existing);
    existing.UpdatedAt = DateTime.UtcNow;
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Обновлены данные подразделения '{SubdivisionName}' (ID: {SubdivisionId})",
        subdivision.Name, subdivision.Id);
  }

  public async Task MoveAsync(Guid id, Guid? newParentId, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Subdivisions.Write, ct);
    await _uow.ExecuteInTransactionAsync(async () =>
    {
      var target = await GetRequiredForWriteAsync(_uow, id, ct);
      if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(target.Version, expectedVersion.Value);
      if (!(await _scopes.GetAsync(ct)).Allows(newParentId)) throw new UnauthorizedAccessException("Новый родитель недоступен.");

      // Нет изменений родителя — выходим
      if (target.ParentId == newParentId)
        return;

      if (target.Status is not (Status.Inserted or Status.Modified)) throw new EntityRuleException("Подразделение заблокировано или неактивно.");
      string oldPath = target.Path;
      if (string.IsNullOrWhiteSpace(oldPath)) throw new EntityRuleException("Путь подразделения не настроен.");
      string newParentPath = "/";

      if (newParentId.HasValue)
      {
        // 1. Проверка попытки назначить родителем самого себя
        if (newParentId.Value == target.Id)
          throw new EntityRuleException("Нельзя переместить подразделение в самого себя.");

        var newParent = await _uow.BaseEntityRepository<Subdivision>().GetByIdAsync(newParentId.Value, ct);
        if (newParent == null)
          throw new KeyNotFoundException($"Новое родительское подразделение с ID '{newParentId}' не найдено.");

        // 2. Проверка циклической зависимости (нельзя переместить родителя в его потомка)
        if (!string.IsNullOrEmpty(oldPath) && newParent.Path.StartsWith(oldPath, StringComparison.OrdinalIgnoreCase))
          throw new EntityRuleException("Нельзя переместить подразделение в одного из его подчиненных узлов.");

        newParentPath = newParent.Path;
      }

      // Новый базовый путь целевого подразделения
      string newPath = $"{newParentPath}{target.SubdivisionId}/";

      // 3. Выборка целевого узла и ВСЕХ его потомков через Prefix Match (StartsWith)
      var repo = _uow.BaseEntityRepository<Subdivision>();
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

  public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Subdivisions.Write, ct);
    var subdivision = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(subdivision.Version, expectedVersion.Value);

    await _uow.BaseEntityRepository<Subdivision>().ChangeStatusAsync(subdivision, EntityStatusTransitions.Apply(subdivision.Status, StatusOperation.Delete, (await _permissions.GetCurrentAsync(ct)).IsRoot), ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Подразделение '{SubdivisionName}' (ID: {SubdivisionId}) помечено как удаленное",
        subdivision.Name, id);
  }

  public async Task ArchiveAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Subdivisions.Write, ct);
    var subdivision = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(subdivision.Version, expectedVersion.Value);

    await _uow.BaseEntityRepository<Subdivision>().ChangeStatusAsync(subdivision, EntityStatusTransitions.Apply(subdivision.Status, StatusOperation.Archive, (await _permissions.GetCurrentAsync(ct)).IsRoot), ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Подразделение '{SubdivisionName}' (ID: {SubdivisionId}) отправлено в архив",
        subdivision.Name, id);
  }

  public async Task BlockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Subdivisions.Write, ct);
    var subdivision = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(subdivision.Version, expectedVersion.Value);

    await _uow.BaseEntityRepository<Subdivision>().ChangeStatusAsync(subdivision, EntityStatusTransitions.Apply(subdivision.Status, StatusOperation.Block, (await _permissions.GetCurrentAsync(ct)).IsRoot), ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Подразделение '{SubdivisionName}' (ID: {SubdivisionId}) заблокировано",
        subdivision.Name, id);
  }

  public async Task UnblockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Subdivisions.Write, ct);
    var subdivision = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(subdivision.Version, expectedVersion.Value);

    await _uow.BaseEntityRepository<Subdivision>().ChangeStatusAsync(subdivision, EntityStatusTransitions.Apply(subdivision.Status, StatusOperation.Unblock, (await _permissions.GetCurrentAsync(ct)).IsRoot), ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Подразделение '{SubdivisionName}' (ID: {SubdivisionId}) разблокировано",
        subdivision.Name, id);
  }

  public async Task RestoreAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Subdivisions.Write, ct);
    var subdivision = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(subdivision.Version, expectedVersion.Value);

    await _uow.BaseEntityRepository<Subdivision>().ChangeStatusAsync(subdivision, EntityStatusTransitions.Apply(subdivision.Status, StatusOperation.Restore, (await _permissions.GetCurrentAsync(ct)).IsRoot), ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Подразделение '{SubdivisionName}' (ID: {SubdivisionId}) восстановлено",
        subdivision.Name, id);
  }

  // ==================== Private Helpers ====================

  private async Task<Subdivision> GetRequiredForWriteAsync(IUnitOfWork _uow, Guid id, CancellationToken ct)
  {
    var subdivision = await _uow.BaseEntityRepository<Subdivision>().GetByIdAsync(id, ct);
    var scope = await _scopes.GetAsync(ct);
    if (subdivision == null || !scope.Allows(id) || subdivision.Status == Status.Deleted && !scope.IsRoot)
    {
      _logger.LogWarning("Попытка выполнения операции над несуществующим подразделением (ID: {SubdivisionId})", id);
      throw new KeyNotFoundException($"Подразделение с ID '{id}' не найдено.");
    }

    if (subdivision.Status is Status.Archived or Status.ArchivedBlocked)
      await _permissions.RequireAsync(Permissions.Subdivisions.ReadArchive, ct);
    return subdivision;
  }

  public async Task RebuildHierarchyAndPathsAsync(CancellationToken ct = default)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Subdivisions.Write, ct);
    if (!(await _permissions.GetCurrentAsync(ct)).IsRoot) throw new UnauthorizedAccessException("Перестроение доступно только Root.");
    await _uow.ExecuteInTransactionAsync(async () =>
    {
      var repo = _uow.BaseEntityRepository<Subdivision>();

      // 1. Загружаем все подразделения из базы данных
      var allSubdivisions = await repo.Query().ToListAsync(ct);

      if (allSubdivisions.Count == 0)
      {
        _logger.LogInformation("Список подразделений пуст. Пересчет иерархии пропущен.");
        return;
      }

      // 2. Создаем быстрый словарь по long-идентификатору SubdivisionId
      var dictionary = allSubdivisions
          .Where(s => s.SubdivisionId > 0)
          .ToDictionary(s => s.SubdivisionId);

      // Группировка детей по Guid родителя для быстрой рекурсии в памяти
      var childrenLookup = new Dictionary<Guid, List<Subdivision>>();
      var rootNodes = new List<Subdivision>();

      // 3. Связываем ParentId (Guid) на основе ParentSubdivisionId (long)
      foreach (var item in allSubdivisions)
      {
        if (item.ParentSubdivisionId > 0
            && item.ParentSubdivisionId != item.SubdivisionId
            && dictionary.TryGetValue(item.ParentSubdivisionId, out var parent))
        {
          item.ParentId = parent.Id;

          if (!childrenLookup.ContainsKey(parent.Id))
          {
            childrenLookup[parent.Id] = new List<Subdivision>();
          }

          childrenLookup[parent.Id].Add(item);
        }
        else
        {
          item.ParentId = null;
          rootNodes.Add(item);
        }
      }

      // 4. Локальная функция для рекурсивного расчета Path (например, /1/4/12/)
      void BuildPathRecursive(Subdivision node, string parentPath)
      {
        node.Path = string.IsNullOrEmpty(parentPath)
            ? $"/{node.SubdivisionId}/"
            : $"{parentPath}{node.SubdivisionId}/";

        if (childrenLookup.TryGetValue(node.Id, out var children))
        {
          foreach (var child in children)
          {
            BuildPathRecursive(child, node.Path);
          }
        }
      }

      // 5. Запускаем обход дерева с корневых узлов
      foreach (var root in rootNodes)
      {
        BuildPathRecursive(root, string.Empty);
      }

      // 6. Помечаем сущности для обновления и сохраняем изменения
      foreach (var item in allSubdivisions)
      {
        await repo.UpdateAsync(item, ct);
      }

      await _uow.SaveChangesAsync(ct);

      _logger.LogInformation("Успешно перестроена иерархия и Path для {Count} подразделений.", allSubdivisions.Count);
    }, ct);
  }

  // ==================== Справочники (Read) ====================

  public async Task<IReadOnlyList<OrganType>> GetOrganTypesAsync(CancellationToken ct = default)
  {
    await _permissions.RequireAsync(Permissions.Subdivisions.Write, ct);
    await using var db = await _factory.CreateDbContextAsync(ct);
    return await db.Set<OrganType>().AsNoTracking().OrderBy(o => o.Name).ToListAsync(ct);
  }

  public async Task<IReadOnlyList<Classifier>> GetClassifiersByTypeAsync(ClassifierType type, CancellationToken ct = default)
  {
    await _permissions.RequireAsync(Permissions.Subdivisions.Write, ct);
    await using var db = await _factory.CreateDbContextAsync(ct);
    return await db.Set<Classifier>().AsNoTracking().Where(c => c.Type == (int)type).OrderBy(c => c.Value).ToListAsync(ct);
  }

  public Task<Guid> CreateFromDtoAsync(CreateSubdivisionDto input, CancellationToken ct = default) {
    ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(input.Fields);
    var entity = new Subdivision(); input.Fields.ApplyTo(entity); entity.ParentId = input.ParentId;
    return CreateAsync(entity, ct);
  }
  public async Task UpdateFromDtoAsync(EditSubdivisionDto input, CancellationToken ct = default) {
    ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(input.Fields);
    var entity = await GetByIdAsync(input.Id, ct) ?? throw new KeyNotFoundException("Подразделение недоступно.");
    input.Fields.ApplyTo(entity); entity.Version = input.Version;
    await UpdateAsync(entity, ct);
  }
}
