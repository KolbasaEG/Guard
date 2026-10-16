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
  private readonly IReadRepository<Classifier> _readClassifierRepository;
  private readonly IUnitOfWorkFactory _writes;
  private readonly ILogger<SubdivisionService> _logger;
  private readonly IDbContextFactory<ApplicationDbContext> _factory;
  private readonly IPermissionService _permissions;
  private readonly IDataAccessScopeService _scopes;

  public SubdivisionService(
      IReadRepository<Subdivision> readSubdivisionRepository,
      IReadRepository<Classifier> readClassifierRepository,
      IUnitOfWorkFactory unitOfWork,
      ILogger<SubdivisionService> logger, IPermissionService permissions, IDataAccessScopeService scopes, IDbContextFactory<ApplicationDbContext> factory)
  {
    _readSubdivisionRepository = readSubdivisionRepository;
    _readClassifierRepository = readClassifierRepository;
    _writes = unitOfWork;
    _logger = logger;
    _factory = factory;
    _permissions = permissions;
    _scopes = scopes;
  }

  // ==================== Read (Изолированный IReadRepository) ====================
  public async Task<SubdivisionDetailsDto?> GetDetailsAsync(Guid id, CancellationToken ct = default)
  {
    var (user, scope) = await GetDialogAccessAsync(false, ct);
    await using var db = await _factory.CreateDbContextAsync(ct);
    var result = await DialogSubdivisions(db, scope).Where(s => s.Id == id &&
      (s.Status == Status.Inserted || s.Status == Status.Modified || s.Status == Status.Blocked ||
       (s.Status == Status.Archived || s.Status == Status.ArchivedBlocked) && user.Has(Permissions.Subdivisions.ReadArchive) ||
       s.Status == Status.Deleted && user.IsRoot))
      .Select(s => new SubdivisionDetailsDto {
        Id = s.Id, Version = s.Version, Status = s.Status, ParentId = s.ParentId,
        ParentName = s.Parent == null ? null : s.Parent.Name,
        OrganTypeName = s.OrganType == null ? null : s.OrganType.Value,
        StatusClassifierName = s.StatusClassifier == null ? null : s.StatusClassifier.Value,
        Name = s.Name, PositionFormationName = s.PositionFormationName, PostalCode = s.PostalCode,
        Address = s.Address, Phone = s.Phone, Fax = s.Fax, StaffCount = s.StaffCount,
        LevelOrder = s.LevelOrder, IsDepartment = s.IsDepartment, StatusType = s.StatusType,
        StatusCode = s.StatusCode, OrganTypeId = s.OrganTypeId, OrganTypeCode = s.OrganTypeCode,
        Info = new EntityInfoDto {
          Id = s.Id, InsertedDate = s.InsertedDate, LastModifiedDate = s.LastModifiedDate,
          Creator = db.Users.Where(u => u.Id == s.CreatedBy).Select(u => u.Email).FirstOrDefault(),
          Editor = db.Users.Where(u => u.Id == s.ModifiedBy).Select(u => u.Email).FirstOrDefault()
        }
      }).SingleOrDefaultAsync(ct);
    await EnsureDialogAccessAsync(user, scope, false, ct);
    return result;
  }

  public async Task<SubdivisionEditorOptionsDto> GetEditorOptionsAsync(CancellationToken ct = default)
  {
    var (user, scope) = await GetDialogAccessAsync(true, ct);
    await using var db = await _factory.CreateDbContextAsync(ct);
    var parents = await ParentOptions(DialogSubdivisions(db, scope)).ToListAsync(ct);
    var organs = await db.Classifiers.AsNoTracking().Where(c => c.Type == 363 && c.IsActive)
      .OrderBy(c => c.Value).ThenBy(c => c.Code).Select(c => new SubdivisionClassifierOptionDto(c.Type, c.Code, c.Value)).ToListAsync(ct);
    // Статусы, включая неактивные, сохраняют прежний набор вариантов формы.
    var statuses = await db.Classifiers.AsNoTracking().Where(c => c.Type == (int)ClassifierType.СтатусПодразделения)
      .OrderBy(c => c.Value).ThenBy(c => c.Code).Select(c => new SubdivisionClassifierOptionDto(c.Type, c.Code, c.Value)).ToListAsync(ct);
    await EnsureDialogAccessAsync(user, scope, true, ct);
    return new(parents, organs, statuses);
  }

  public async Task<IReadOnlyList<SubdivisionParentOptionDto>> GetMoveParentOptionsAsync(Guid id, CancellationToken ct = default)
  {
    var (user, scope) = await GetDialogAccessAsync(true, ct);
    await using var db = await _factory.CreateDbContextAsync(ct);
    var target = await DialogSubdivisions(db, scope).Where(s => s.Id == id)
      .Select(s => new { s.Path, s.Status }).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Подразделение недоступно.");
    if (target.Status is not (Status.Inserted or Status.Modified)) throw new EntityRuleException("Подразделение неактивно или заблокировано.");
    if (!DataAccessScopeService.IsValidPath(target.Path)) throw new EntityRuleException("Путь подразделения не настроен.");
    var result = await ParentOptions(DialogSubdivisions(db, scope).Where(s => s.Id != id && !s.Path.StartsWith(target.Path))).ToListAsync(ct);
    await EnsureDialogAccessAsync(user, scope, true, ct);
    return result;
  }

  private static IQueryable<Subdivision> DialogSubdivisions(ApplicationDbContext db, DataAccessScope scope) =>
    scope.IsRoot ? db.Subdivisions.AsNoTracking() : db.Subdivisions.AsNoTracking().Where(s => scope.SubdivisionIds.Contains(s.Id));

  private static IQueryable<SubdivisionParentOptionDto> ParentOptions(IQueryable<Subdivision> query) => query
    .Where(s => s.Status == Status.Inserted || s.Status == Status.Modified)
    .OrderBy(s => s.Name).ThenBy(s => s.Id).Select(s => new SubdivisionParentOptionDto(s.Id, s.Name, s.Path));

  private async Task<(UserAccessSnapshot User, DataAccessScope Scope)> GetDialogAccessAsync(bool write, CancellationToken ct)
  {
    var user = await _permissions.GetCurrentAsync(ct);
    if (!user.Has(Permissions.Subdivisions.Read) || write && !user.Has(Permissions.Subdivisions.Write))
      throw new UnauthorizedAccessException("Недостаточно прав для работы с подразделениями.");
    var scope = await _scopes.GetAsync(ct);
    if (scope.UserId != user.UserId || scope.IsRoot != user.IsRoot) throw new UnauthorizedAccessException("Область доступа изменилась.");
    return (user, scope);
  }

  private async Task EnsureDialogAccessAsync(UserAccessSnapshot user, DataAccessScope scope, bool write, CancellationToken ct)
  {
    var (final, finalScope) = await GetDialogAccessAsync(write, ct);
    ct.ThrowIfCancellationRequested();
    if (user.UserId != final.UserId || user.IsRoot != final.IsRoot || !user.Permissions.SetEquals(final.Permissions) ||
        !scope.SubdivisionIds.SetEquals(finalScope.SubdivisionIds))
      throw new UnauthorizedAccessException("Область доступа изменилась во время загрузки.");
  }

  public async Task<SubdivisionSearchResult> SearchAsync(SubdivisionSearchRequest request, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    if (request.Skip < 0 || request.Take is < 1 or > 100) throw new ArgumentException("Недопустимые параметры страницы.");
    SubdivisionSearchQuery.ValidateFilter(request.Filter);
    var initial = await _permissions.GetCurrentAsync(ct);
    RequireSearchAccess(initial, request.Mode);
    var scope = await _scopes.GetAsync(ct);
    if (scope.UserId != initial.UserId || scope.IsRoot != initial.IsRoot) throw new UnauthorizedAccessException("Область доступа изменилась.");
    await using var db = await _factory.CreateDbContextAsync(ct);
    async Task<string?> OwnPath(string userId) => await db.Users.Where(u => u.Id == userId)
      .Select(u => u.Personal == null || u.Personal.Subdivision == null ? null : u.Personal.Subdivision.Path).SingleOrDefaultAsync(ct);
    var path = await OwnPath(initial.UserId);
    var query = db.Subdivisions.AsNoTracking().AsQueryable();
    if (!scope.IsRoot) query = query.Where(s => scope.SubdivisionIds.Contains(s.Id));
    query = SubdivisionSearchQuery.Filter(SubdivisionSearchQuery.Visibility(query, request.Mode,
      request.AllSubdivisions, scope.IsRoot, path, request.Hierarchy), request.Filter);
    var sorted = SubdivisionSearchQuery.Sort(query, request.OrderBy);
    var total = await query.CountAsync(ct);
    var items = await sorted.Skip(request.Skip).Take(request.Take).Select(p => new SubdivisionListDto {
      Id = p.Id, Version = p.Version, Status = p.Status, Name = p.Name,
      OrganTypeName = p.OrganType == null ? null : p.OrganType.Value,
      StatusClassifierName = p.StatusClassifier == null ? null : p.StatusClassifier.Value,
      ParentName = p.Parent == null ? null : p.Parent.Name, PositionFormationName = p.PositionFormationName,
      PostalCode = p.PostalCode, Address = p.Address, Phone = p.Phone, StaffCount = p.StaffCount,
      LevelOrder = p.LevelOrder, IsDepartment = p.IsDepartment
    }).ToListAsync(ct);
    var final = await _permissions.GetCurrentAsync(ct);
    RequireSearchAccess(final, request.Mode);
    var finalScope = await _scopes.GetAsync(ct);
    var finalPath = await OwnPath(final.UserId);
    ct.ThrowIfCancellationRequested();
    if (initial.UserId != final.UserId || initial.IsRoot != final.IsRoot || path != finalPath ||
        scope.UserId != finalScope.UserId || scope.IsRoot != finalScope.IsRoot || !scope.SubdivisionIds.SetEquals(finalScope.SubdivisionIds))
      throw new UnauthorizedAccessException("Область доступа изменилась во время загрузки.");
    return new(items, total);
  }

  private static void RequireSearchAccess(UserAccessSnapshot user, DataViewMode mode)
  {
    if (!user.Has(Permissions.Subdivisions.Read) || mode == DataViewMode.Archived && !user.Has(Permissions.Subdivisions.ReadArchive) ||
        mode == DataViewMode.Deleted && !user.IsRoot) throw new UnauthorizedAccessException("Недостаточно прав для выбранного режима подразделений.");
  }

  public async Task<string?> GetOwnSubdivisionPathAsync(CancellationToken ct = default)
  {
    await _permissions.RequireAsync(Permissions.Subdivisions.Read, ct);
    var user = await _permissions.GetCurrentAsync(ct);
    await using var db = await _factory.CreateDbContextAsync(ct);
    return await db.Users.Where(u => u.Id == user.UserId)
      .Select(u => u.Personal == null || u.Personal.Subdivision == null ? null : u.Personal.Subdivision.Path)
      .SingleOrDefaultAsync(ct);
  }

  public async Task<TResult> QuerySubdivisionsAsync<TResult>(
        Func<IQueryable<Subdivision>, Task<TResult>> query,
        CancellationToken ct = default)
  {
    return await _readSubdivisionRepository.QueryAsync(query, ct);
  }

  public async Task<Subdivision?> GetByIdAsync(Guid id, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос подразделения по ID: {SubdivisionId}", id);
    return await _readSubdivisionRepository.QueryAsync(q => q.Include(s => s.Parent).Include(s => s.OrganType).Include(s => s.StatusClassifier).SingleOrDefaultAsync(s => s.Id == id, ct), ct);
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
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);

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
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);

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
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);
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
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);
    var subdivision = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(subdivision.Version, expectedVersion.Value);

    await _uow.BaseEntityRepository<Subdivision>().ChangeStatusAsync(subdivision, EntityStatusTransitions.ApplyManual(subdivision, StatusOperation.Delete, (await _permissions.GetCurrentAsync(ct)).IsRoot), ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Подразделение '{SubdivisionName}' (ID: {SubdivisionId}) помечено как удаленное",
        subdivision.Name, id);
  }

  public async Task ArchiveAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Subdivisions.Write, ct);
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);
    var subdivision = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(subdivision.Version, expectedVersion.Value);

    await _uow.BaseEntityRepository<Subdivision>().ChangeStatusAsync(subdivision, EntityStatusTransitions.ApplyManual(subdivision, StatusOperation.Archive, (await _permissions.GetCurrentAsync(ct)).IsRoot), ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Подразделение '{SubdivisionName}' (ID: {SubdivisionId}) отправлено в архив",
        subdivision.Name, id);
  }

  public async Task BlockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Subdivisions.Write, ct);
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);
    var subdivision = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(subdivision.Version, expectedVersion.Value);

    await _uow.BaseEntityRepository<Subdivision>().ChangeStatusAsync(subdivision, EntityStatusTransitions.ApplyManual(subdivision, StatusOperation.Block, (await _permissions.GetCurrentAsync(ct)).IsRoot), ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Подразделение '{SubdivisionName}' (ID: {SubdivisionId}) заблокировано",
        subdivision.Name, id);
  }

  public async Task UnblockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Subdivisions.Write, ct);
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);
    var subdivision = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(subdivision.Version, expectedVersion.Value);

    await _uow.BaseEntityRepository<Subdivision>().ChangeStatusAsync(subdivision, EntityStatusTransitions.ApplyManual(subdivision, StatusOperation.Unblock, (await _permissions.GetCurrentAsync(ct)).IsRoot), ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Подразделение '{SubdivisionName}' (ID: {SubdivisionId}) разблокировано",
        subdivision.Name, id);
  }

  public async Task RestoreAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Subdivisions.Write, ct);
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);
    var subdivision = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(subdivision.Version, expectedVersion.Value);

    await _uow.BaseEntityRepository<Subdivision>().ChangeStatusAsync(subdivision, EntityStatusTransitions.ApplyManual(subdivision, StatusOperation.Restore, (await _permissions.GetCurrentAsync(ct)).IsRoot), ct);
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
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);
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

  public async Task<IReadOnlyList<Classifier>> GetOrganTypesAsync(CancellationToken ct = default)
  {
    await _permissions.RequireAsync(Permissions.Subdivisions.Write, ct);
    await using var db = await _factory.CreateDbContextAsync(ct);
    return await db.Classifiers.AsNoTracking().Where(c => c.Type == 363 && c.IsActive).OrderBy(c => c.Value).ToListAsync(ct);
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
