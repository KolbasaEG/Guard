using Guard.Core.Services.DTOs;
using Guard.Core.Identity;
using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public class ClassifierService : IClassifierService
{
  private readonly IReadRepository<Classifier> _readClassifierRepository;
  private readonly IUnitOfWorkFactory _writes;
  private readonly ILogger<ClassifierService> _logger;
  private readonly IPermissionService _permissions;

  public ClassifierService(
      IReadRepository<Classifier> readClassifierRepository,
      IUnitOfWorkFactory unitOfWork,
      ILogger<ClassifierService> logger, IPermissionService permissions)
  {
    _readClassifierRepository = readClassifierRepository;
    _writes = unitOfWork;
    _logger = logger;
    _permissions = permissions;
  }

  // ==================== Read (Изолированный IReadRepository) ====================

  private async Task<UserAccessSnapshot> RequireReadAsync(CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();
    var access = await _permissions.GetCurrentAsync(ct);
    if (!access.Has(Permissions.Classifiers.Read)) throw new UnauthorizedAccessException();
    return access with { Permissions = new(access.Permissions) };
  }

  private async Task RecheckReadAsync(UserAccessSnapshot access, CancellationToken ct)
  {
    var current = await RequireReadAsync(ct);
    if (access.UserId != current.UserId || access.IsRoot != current.IsRoot || !access.Permissions.SetEquals(current.Permissions))
      throw new UnauthorizedAccessException("Доступ изменился. Повторите загрузку.");
    ct.ThrowIfCancellationRequested();
  }

  public async Task<ClassifierSearchResult> SearchAsync(ClassifierSearchRequest request, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    var access = await RequireReadAsync(ct);
    if (request.Skip < 0 || request.Take is < 1 or > 100) throw new ArgumentException("Недопустимые параметры страницы.");
    var result = await _readClassifierRepository.QueryAsync(async query => {
      query = ClassifierSearchQuery.Filter(query, request.Filter);
      var count = await query.CountAsync(ct);
      var items = await ClassifierSearchQuery.Sort(query, request.OrderBy).Skip(request.Skip).Take(request.Take)
        .Select(c => new ClassifierListDto(c.Id, c.Version, c.Type, c.Code, c.ClassifierName, c.Value, c.IsActive, c.UpdatedAt)).ToListAsync(ct);
      return new ClassifierSearchResult(items, count);
    }, ct);
    await RecheckReadAsync(access, ct);
    return result;
  }

  public async Task<ClassifierDetailsDto?> GetDetailsAsync(int id, CancellationToken ct = default)
  {
    var access = await RequireReadAsync(ct);
    var result = await _readClassifierRepository.QueryAsync(query => query.Where(c => c.Id == id)
      .Select(c => new ClassifierDetailsDto { Id = c.Id, Version = c.Version, Type = c.Type, Code = c.Code,
        ClassifierName = c.ClassifierName, Value = c.Value, IsActive = c.IsActive, UpdatedAt = c.UpdatedAt })
      .SingleOrDefaultAsync(ct), ct);
    await RecheckReadAsync(access, ct);
    return result;
  }

  public async Task<TResult> QueryClassifiersAsync<TResult>(
      Func<IQueryable<Classifier>, Task<TResult>> query,
      CancellationToken ct = default)
  {
    return await _readClassifierRepository.QueryAsync(query, ct);
  }

  public async Task<Classifier?> GetByIdAsync(int id, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос классификатора по ID: {ClassifierId}", id);
    return await _readClassifierRepository.QueryAsync(query =>
        query.FirstOrDefaultAsync(c => c.Id == id, ct), ct);
  }

  public async Task<IReadOnlyList<Classifier>> GetByTypeAsync(int type, bool activeOnly = true, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос классификаторов по типу: {Type} (ActiveOnly: {ActiveOnly})", type, activeOnly);
    return await _readClassifierRepository.QueryAsync(query =>
        query.Where(c => c.Type == type && (!activeOnly || c.IsActive))
             .OrderBy(c => c.Code)
             .ToListAsync(ct),
        ct);
  }

  public async Task<IReadOnlyList<Classifier>> GetByClassifierNameAsync(string classifierName, bool activeOnly = true, CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(classifierName))
      return Array.Empty<Classifier>();

    var trimmedName = classifierName.Trim();

    _logger.LogDebug("Запрос классификаторов по наименованию: '{ClassifierName}'", trimmedName);
    return await _readClassifierRepository.QueryAsync(query =>
        query.Where(c => c.ClassifierName == trimmedName && (!activeOnly || c.IsActive))
             .OrderBy(c => c.Code)
             .ToListAsync(ct),
        ct);
  }

  public async Task<IReadOnlyList<Classifier>> GetAllAsync(CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос всех записей классификатора");
    return await _readClassifierRepository.QueryAsync(query =>
        query.OrderBy(c => c.Type)
             .ThenBy(c => c.Code)
             .ToListAsync(ct),
        ct);
  }

  public async Task<bool> IsCodeUniqueInTypeAsync(int type, int code, int? excludeId = null, CancellationToken ct = default)
  {
    return await _readClassifierRepository.QueryAsync(query =>
        query.AllAsync(c => c.Type != type || c.Code != code || (excludeId.HasValue && c.Id == excludeId.Value), ct),
        ct);
  }

  // ==================== Write (IUnitOfWork & BasicRepository) ====================

  public async Task<int> CreateAsync(Classifier classifier, CancellationToken ct = default)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Classifiers.Manage, ct);
    ArgumentNullException.ThrowIfNull(classifier);
    var clean = new Classifier { Id = classifier.Id };
    EntityWriteMapping.From(classifier).ApplyTo(clean); classifier = clean;
    EntityInputValidation.Validate(classifier);
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);

    await RequireUniqueCodeAsync(_uow, classifier, ct);

    return await _uow.ExecuteInTransactionAsync(async () =>
    {
      classifier.ClassifierName = classifier.ClassifierName?.Trim() ?? string.Empty;
      classifier.Value = classifier.Value?.Trim() ?? string.Empty;
      classifier.UpdatedAt = DateTime.UtcNow;

      await _uow.BasicRepository<Classifier>().AddAsync(classifier, ct);
      await _uow.SaveChangesAsync(ct);

      _logger.LogInformation("Создана новая запись классификатора '{Value}' (Type: {Type}, Code: {Code}, ID: {ClassifierId})",
          classifier.Value, classifier.Type, classifier.Code, classifier.Id);

      return classifier.Id;
    }, ct);
  }

  public async Task UpdateAsync(Classifier classifier, CancellationToken ct = default)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Classifiers.Manage, ct);
    ArgumentNullException.ThrowIfNull(classifier);
    EntityInputValidation.Validate(classifier);
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);

    classifier.ClassifierName = classifier.ClassifierName?.Trim() ?? string.Empty;
    classifier.Value = classifier.Value?.Trim() ?? string.Empty;
    classifier.UpdatedAt = DateTime.UtcNow;

    var current = await GetRequiredForWriteAsync(_uow, classifier.Id, ct);
    EntityInputValidation.CheckVersion(current.Version, classifier.Version);
    await RequireUniqueCodeAsync(_uow, classifier, ct);
    EntityWriteMapping.From(classifier).ApplyTo(current);
    current.UpdatedAt = DateTime.UtcNow;
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Обновлена запись классификатора '{Value}' (Type: {Type}, Code: {Code}, ID: {ClassifierId})",
        classifier.Value, classifier.Type, classifier.Code, classifier.Id);
  }

  public async Task SetActiveStatusAsync(int id, bool isActive, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Classifiers.Manage, ct);
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);
    var classifier = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(classifier.Version, expectedVersion.Value);

    classifier.IsActive = isActive;
    classifier.UpdatedAt = DateTime.UtcNow;

    await _uow.BasicRepository<Classifier>().UpdateAsync(classifier, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Статус активности классификатора ID {ClassifierId} изменен на: {IsActive}", id, isActive);
  }

  public async Task DeleteAsync(int id, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.Classifiers.Manage, ct);
    await DirectoryManagementService.RequireLocalAsync(_uow, ct);
    var classifier = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(classifier.Version, expectedVersion.Value);

    await _uow.BasicRepository<Classifier>().DeleteAsync(classifier, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Запись классификатора '{Value}' (ID: {ClassifierId}) была физически удалена из системы",
        classifier.Value, id);
  }

  // ==================== Private Helpers ====================

  private static async Task RequireUniqueCodeAsync(IUnitOfWork work, Classifier classifier, CancellationToken ct)
  {
    if (await work.BasicRepository<Classifier>().Query().AnyAsync(c => c.Type == classifier.Type && c.Code == classifier.Code && c.Id != classifier.Id, ct))
      throw new ArgumentException($"Код {classifier.Code} уже используется для типа справочника {classifier.Type}.");
  }

  private async Task<Classifier> GetRequiredForWriteAsync(IUnitOfWork _uow, int id, CancellationToken ct)
  {
    var classifier = await _uow.BasicRepository<Classifier>().GetByIdAsync([id], ct);

    if (classifier == null)
    {
      _logger.LogWarning("Попытка выполнения операции над несуществующей записью классификатора (ID: {ClassifierId})", id);
      throw new KeyNotFoundException($"Запись классификатора с ID '{id}' не найдена.");
    }

    return classifier;
  }

  public Task<int> CreateFromDtoAsync(CreateClassifierDto input, CancellationToken ct = default) {
    ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(input.Fields);
    var entity = new Classifier(); input.Fields.ApplyTo(entity);
    return CreateAsync(entity, ct);
  }
  public async Task UpdateFromDtoAsync(EditClassifierDto input, CancellationToken ct = default) {
    ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(input.Fields);
    var entity = new Classifier { Id = input.Id };
    input.Fields.ApplyTo(entity); entity.Version = input.Version;
    await UpdateAsync(entity, ct);
  }
}
