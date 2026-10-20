using Guard.Core.Identity;
using Guard.Core.Contexts;
using Guard.Core.Services.DTOs;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public class PersonalService : IPersonalSearchService, IPersonalDetailsService, IPersonalStatusService, IPersonalEditorService
{
  private readonly IReadRepository<Personal> _readPersonalRepository;
  private readonly IUnitOfWorkFactory _writes;
  private readonly ILogger<PersonalService> _logger;
  private readonly IDbContextFactory<ApplicationDbContext> _factory;
  private readonly IPermissionService _permissions;
  private readonly IDataAccessScopeService _scopes;

  public PersonalService(
      IReadRepository<Personal> readPersonalRepository,
      IUnitOfWorkFactory unitOfWork,
      ILogger<PersonalService> logger, IPermissionService permissions, IDataAccessScopeService scopes, IDbContextFactory<ApplicationDbContext> factory)
  {
    _readPersonalRepository = readPersonalRepository;

    _writes = unitOfWork;
    _logger = logger;
    _factory = factory;
    _permissions = permissions;
    _scopes = scopes;
  }
  public async Task<PersonalEditorDto?> GetEditorAsync(Guid id, CancellationToken ct = default)
  {
    var actor = await SearchAccess.RequireAsync(_permissions, Permissions.Personals.Write, ct);
    await _permissions.RequireAsync(Permissions.Personals.ReadDetails, ct);
    var result = await _readPersonalRepository.QueryAsync(q => q.Where(p => p.Id == id)
      .Select(p => new PersonalEditorDto {
      Id = p.Id, Version = p.Version, Status = p.Status,
      SubdivisionId = p.SubdivisionId,
      LastName = p.LastName,
      FirstName = p.FirstName,
      MiddleName = p.MiddleName,
      FullName = p.FullName,
      EnlistmentYear = p.EnlistmentYear,
      PersonalNumber = p.PersonalNumber,
      LastNameGen = p.LastNameGen,
      FirstNameGen = p.FirstNameGen,
      MiddleNameGen = p.MiddleNameGen,
      PersonnelCategoryType = p.PersonnelCategoryType,
      PersonnelCategoryCode = p.PersonnelCategoryCode,
      SpecialRankType = p.SpecialRankType,
      SpecialRankCode = p.SpecialRankCode,
      PositionType = p.PositionType,
      PositionCode = p.PositionCode,
      WorkerCategoryType = p.WorkerCategoryType,
      WorkerCategoryCode = p.WorkerCategoryCode,
      StatusType = p.StatusType,
      StatusCode = p.StatusCode
      }).SingleOrDefaultAsync(ct), ct);
    await SearchAccess.RecheckAsync(_permissions, Permissions.Personals.Write, actor, ct);
    await _permissions.RequireAsync(Permissions.Personals.ReadDetails, ct);
    if (result != null && !(await _scopes.GetAsync(ct)).Allows(result.SubdivisionId))
      throw new UnauthorizedAccessException("Подразделение недоступно.");
    if (result != null && result.Status is not (Status.Inserted or Status.Modified))
      throw new EntityRuleException("Редактирование недоступно: сотрудник заблокирован, архивирован или удалён.");
    ct.ThrowIfCancellationRequested();
    return result;
  }

  public async Task<IReadOnlyList<PersonalSubdivisionOptionDto>> GetSubdivisionOptionsAsync(CancellationToken ct = default)
  {
    var actor = await SearchAccess.RequireAsync(_permissions, Permissions.Personals.Write, ct);
    var scope = await _scopes.GetAsync(ct);
    await using var db = await _factory.CreateDbContextAsync(ct);
    var result = await db.Set<Subdivision>().AsNoTracking()
      .Where(s => (scope.IsRoot || scope.SubdivisionIds.Contains(s.Id)) &&
        (s.Status == Status.Inserted || s.Status == Status.Modified))
      .OrderBy(s => s.Name).ThenBy(s => s.Id)
      .Select(s => new PersonalSubdivisionOptionDto(s.Id, s.Name)).ToListAsync(ct);
    await SearchAccess.RecheckAsync(_permissions, Permissions.Personals.Write, actor, ct);
    var currentScope = await _scopes.GetAsync(ct);
    if (result.Any(s => !currentScope.Allows(s.Id)))
      throw new UnauthorizedAccessException("Область доступа изменилась. Повторите загрузку.");
    ct.ThrowIfCancellationRequested();
    return result;
  }
  // ==================== Write (IUnitOfWork & ChangeTracker) ====================

  private async Task<Guid> CreateAsync(Personal Personal, CancellationToken ct = default)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Personals.Write, ct);
    ArgumentNullException.ThrowIfNull(Personal);
    var clean = new Personal { Id = Personal.Id };
    EntityWriteMapping.From(Personal).ApplyTo(clean); Personal = clean;
    EntityInputValidation.Validate(Personal);
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);

    if (!(await _scopes.GetAsync(ct)).Allows(Personal.SubdivisionId)) throw new UnauthorizedAccessException("Подразделение недоступно.");
    Personal.Status = Status.Inserted;
    await EntityReferences.PersonalAsync(_uow, Personal, null, ct);

    await _uow.BaseEntityRepository<Personal>().AddAsync(Personal, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Создана запись сотрудника {PersonalId}", Personal.Id);

    return Personal.Id;
  }

  private async Task UpdateAsync(Personal Personal, CancellationToken ct = default)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Personals.Write, ct);
    ArgumentNullException.ThrowIfNull(Personal);
    EntityInputValidation.Validate(Personal);
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);

    var current = await GetRequiredForWriteAsync(_uow, Personal.Id, ct) ?? throw new KeyNotFoundException("Сотрудник недоступен.");
    if (current.Status is not (Status.Inserted or Status.Modified)) throw new EntityRuleException("Сотрудник заблокирован или неактивен.");
    if (!(await _scopes.GetAsync(ct)).Allows(Personal.SubdivisionId)) throw new UnauthorizedAccessException("Подразделение недоступно.");
    Personal.Status = current.Status;
    Personal.UpdatedAt = DateTime.UtcNow;

    EntityInputValidation.CheckVersion(current.Version, Personal.Version);
    await EntityReferences.PersonalAsync(_uow, Personal, current, ct);
    EntityWriteMapping.From(Personal).ApplyTo(current);
    current.UpdatedAt = DateTime.UtcNow;
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Обновлены данные сотрудника {PersonalId}", Personal.Id);
  }

  // ==================== Status Management ====================

  public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Personals.Write, ct);
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);
    var Personal = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(Personal.Version, expectedVersion.Value);

    await _uow.BaseEntityRepository<Personal>().ChangeStatusAsync(Personal, EntityStatusTransitions.ApplyManual(Personal, StatusOperation.Delete, (await _permissions.GetCurrentAsync(ct)).IsRoot), ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Сотрудник {PersonalId} помечен как удаленный", id);
  }

  public async Task ArchiveAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Personals.Write, ct);
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);
    var Personal = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(Personal.Version, expectedVersion.Value);

    await _uow.BaseEntityRepository<Personal>().ChangeStatusAsync(Personal, EntityStatusTransitions.ApplyManual(Personal, StatusOperation.Archive, (await _permissions.GetCurrentAsync(ct)).IsRoot), ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Сотрудник {PersonalId} отправлен в архив", id);
  }

  public async Task BlockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Personals.Write, ct);
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);
    var Personal = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(Personal.Version, expectedVersion.Value);

    await _uow.BaseEntityRepository<Personal>().ChangeStatusAsync(Personal, EntityStatusTransitions.ApplyManual(Personal, StatusOperation.Block, (await _permissions.GetCurrentAsync(ct)).IsRoot), ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Сотрудник {PersonalId} заблокирован", id);
  }

  public async Task UnblockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Personals.Write, ct);
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);
    var Personal = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(Personal.Version, expectedVersion.Value);

    await _uow.BaseEntityRepository<Personal>().ChangeStatusAsync(Personal, EntityStatusTransitions.ApplyManual(Personal, StatusOperation.Unblock, (await _permissions.GetCurrentAsync(ct)).IsRoot), ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Сотрудник {PersonalId} разблокирован", id);
  }

  public async Task RestoreAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Personals.Write, ct);
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);
    var Personal = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(Personal.Version, expectedVersion.Value);

    await _uow.BaseEntityRepository<Personal>().ChangeStatusAsync(Personal, EntityStatusTransitions.ApplyManual(Personal, StatusOperation.Restore, (await _permissions.GetCurrentAsync(ct)).IsRoot), ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Сотрудник {PersonalId} восстановлен", id);
  }

  // ==================== Private Helpers ====================

  private async Task<Personal> GetRequiredForWriteAsync(IUnitOfWork _uow, Guid id, CancellationToken ct)
  {
    var Personal = await _uow.BaseEntityRepository<Personal>().GetByIdAsync(id, ct);
    var scope = await _scopes.GetAsync(ct);
    if (Personal == null || !scope.Allows(Personal.SubdivisionId) || Personal.Status == Status.Deleted && !scope.IsRoot)
    {
      _logger.LogWarning("Попытка выполнения операции над несуществующей записью персонала (ID: {PersonalId})", id);
      throw new KeyNotFoundException($"Сотрудник с ID '{id}' не найден.");
    }

    if (Personal.Status is Status.Archived or Status.ArchivedBlocked)
      await _permissions.RequireAsync(Permissions.Personals.ReadArchive, ct);
    return Personal;
  }

  private async Task<IQueryable<Personal>> ListQueryAsync(ApplicationDbContext db, PersonalSearchRequest request, CancellationToken ct)
  {
    await _permissions.RequireAsync(Permissions.Personals.Read, ct);
    var actor = await _permissions.GetCurrentAsync(ct);
    var scope = await _scopes.GetAsync(ct);
    return await Guard.Core.Services.Exports.ExportQueries.PersonalsAsync(db, request, actor, scope, ct);
  }
  private static IQueryable<PersonalListItemDto> ProjectList(IQueryable<Personal> query) =>
    query.Select(p => new PersonalListItemDto(p.Id, p.LastName, p.FirstName, p.MiddleName, p.FullName,
        p.Subdivision != null ? p.Subdivision.Name : null, p.Status, p.Version));

  public async Task<PersonalSearchResult> SearchAsync(PersonalSearchRequest request, CancellationToken ct = default)
  {
    if (request.Skip < 0 || request.Take is < 1 or > 100) throw new ArgumentException("Недопустимые параметры страницы.");
    var originalScope = await _scopes.GetAsync(ct);
    await using var db = await _factory.CreateDbContextAsync(ct);
    var q = await ListQueryAsync(db, request, ct);
    var count = await q.CountAsync(ct);
    var items = await ProjectList(q.Skip(request.Skip).Take(request.Take)).ToListAsync(ct);
    await _permissions.RequireAsync(Permissions.Personals.Read, ct);
    await RequireUnchangedScopeAsync(originalScope, ct);
    if (request.Mode == DataViewMode.Archived) await _permissions.RequireAsync(Permissions.Personals.ReadArchive, ct);
    return new(items, count);
  }
  public async Task<PersonalDetailsDto?> GetDetailsAsync(Guid id, CancellationToken ct = default)
  {
    var originalScope = await _scopes.GetAsync(ct);
    var result = await _readPersonalRepository.QueryAsync(q => q.Where(p => p.Id == id).Select(p => new PersonalDetailsDto {
      Id = p.Id,
      Status = p.Status,
      InsertedDate = p.InsertedDate,
      LastModifiedDate = p.LastModifiedDate,
      CreatedBy = p.CreatedBy,
      ModifiedBy = p.ModifiedBy,
      PersonalId = p.PersonalId,
      ExternalId = p.ExternalId,
      SourceUpdatedAt = p.SourceUpdatedAt,
      SubdivisionId = p.SubdivisionId,
      PersonalSubdivisionId = p.PersonalSubdivisionId,
      PersonnelCategoryType = p.PersonnelCategoryType,
      PersonnelCategoryCode = p.PersonnelCategoryCode,
      SpecialRankType = p.SpecialRankType,
      SpecialRankCode = p.SpecialRankCode,
      PositionType = p.PositionType,
      PositionCode = p.PositionCode,
      WorkerCategoryType = p.WorkerCategoryType,
      WorkerCategoryCode = p.WorkerCategoryCode,
      LastName = p.LastName,
      FirstName = p.FirstName,
      MiddleName = p.MiddleName,
      FullName = p.FullName,
      EnlistmentYear = p.EnlistmentYear,
      PersonalNumber = p.PersonalNumber,
      LastNameGen = p.LastNameGen,
      FirstNameGen = p.FirstNameGen,
      MiddleNameGen = p.MiddleNameGen,
      StatusType = p.StatusType,
      StatusCode = p.StatusCode,
      UpdatedAt = p.UpdatedAt,
      SubdivisionName = p.Subdivision == null ? null : p.Subdivision.Name,
      PersonnelCategoryName = p.PersonnelCategory == null ? null : p.PersonnelCategory.Value,
      SpecialRankName = p.SpecialRank == null ? null : p.SpecialRank.Value,
      PositionName = p.Position == null ? null : p.Position.Value,
      WorkerCategoryName = p.WorkerCategory == null ? null : p.WorkerCategory.Value,
      StatusClassifierName = p.StatusClassifier == null ? null : p.StatusClassifier.Value,
      IpAddresses = p.IpAddresses.Select(ip => ip.Address).ToArray()
    }).SingleOrDefaultAsync(ct), ct);
    if (result == null) return null;
    await using var db = await _factory.CreateDbContextAsync(ct);
    var account = await db.Users.AsNoTracking().Where(u => u.PersonalId == id)
      .Select(u => new { u.Id, u.UserName }).SingleOrDefaultAsync(ct);
    await _permissions.RequireAsync(Permissions.Personals.ReadDetails, ct);
    await RequireUnchangedScopeAsync(originalScope, ct);
    if (result.Status is Status.Archived or Status.ArchivedBlocked) await _permissions.RequireAsync(Permissions.Personals.ReadArchive, ct);
    return result with { UserId = account?.Id, UserName = account?.UserName };
  }
  private async Task RequireUnchangedScopeAsync(DataAccessScope original, CancellationToken ct)
  {
    var current = await _scopes.GetAsync(ct);
    if (current.UserId != original.UserId || current.IsRoot != original.IsRoot || !current.SubdivisionIds.SetEquals(original.SubdivisionIds))
      throw new UnauthorizedAccessException("Область доступа изменилась. Обновите данные.");
  }

  public Task<Guid> CreateFromDtoAsync(CreatePersonalDto input, CancellationToken ct = default) {
    ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(input.Fields);
    var entity = new Personal(); input.Fields.ApplyTo(entity);
    return CreateAsync(entity, ct);
  }
  public async Task UpdateFromDtoAsync(EditPersonalDto input, CancellationToken ct = default) {
    ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(input.Fields);
    var entity = new Personal { Id = input.Id };
    input.Fields.ApplyTo(entity); entity.Version = input.Version;
    await UpdateAsync(entity, ct);
  }
}
