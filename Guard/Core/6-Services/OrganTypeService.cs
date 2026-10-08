using Guard.Core.Services.DTOs;
using Guard.Core.Identity;
using Guard.Core.Entities;
using Guard.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public class OrganTypeService : IOrganTypeService
{
  private readonly IReadRepository<OrganType> _readOrganTypeRepository;
  private readonly IUnitOfWorkFactory _writes;
  private readonly ILogger<OrganTypeService> _logger;
  private readonly IPermissionService _permissions;

  public OrganTypeService(
      IReadRepository<OrganType> readOrganTypeRepository,
      IUnitOfWorkFactory unitOfWork,
      ILogger<OrganTypeService> logger, IPermissionService permissions)
  {
    _readOrganTypeRepository = readOrganTypeRepository;
    _writes = unitOfWork;
    _logger = logger;
    _permissions = permissions;
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
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.OrganTypes.Manage, ct);
    ArgumentNullException.ThrowIfNull(organType);
    var clean = new OrganType { Id = organType.Id };
    OrganTypeFieldsDto.From(organType).ApplyTo(clean); organType = clean;
    EntityInputValidation.Validate(organType);

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

      await EntityReferences.OrganTypeAsync(_uow, organType, null, ct);
      await _uow.BasicRepository<OrganType>().AddAsync(organType, ct);
      await _uow.SaveChangesAsync(ct);

      _logger.LogInformation("Создан новый тип органа '{Name}' (Code: {Code}, ID: {OrganTypeId})",
          organType.Name, organType.Code, organType.Id);

      return organType.Id;
    }, ct);
  }

  public async Task UpdateAsync(OrganType organType, CancellationToken ct = default)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.OrganTypes.Manage, ct);
    ArgumentNullException.ThrowIfNull(organType);
    EntityInputValidation.Validate(organType);

    if (!string.IsNullOrWhiteSpace(organType.Name))
    {
      organType.Name = organType.Name.Trim();
    }

    var current = await GetRequiredForWriteAsync(_uow, organType.Id, ct);
    EntityInputValidation.CheckVersion(current.Version, organType.Version);
    await EntityReferences.OrganTypeAsync(_uow, organType, current, ct);
    OrganTypeFieldsDto.From(organType).ApplyTo(current);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Обновлен тип органа '{Name}' (ID: {OrganTypeId})",
        organType.Name, organType.Id);
  }

  public async Task DeleteAsync(int id, CancellationToken ct = default, Guid? expectedVersion = null)
  {
    await using var _uow = await _writes.CreateAsync(ct);
    await _permissions.RequireAsync(Permissions.OrganTypes.Manage, ct);
    var organType = await GetRequiredForWriteAsync(_uow, id, ct);
    if (expectedVersion.HasValue) EntityInputValidation.CheckVersion(organType.Version, expectedVersion.Value);

    await _uow.BasicRepository<OrganType>().DeleteAsync(organType, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Тип органа '{Name}' (ID: {OrganTypeId}) был удален",
        organType.Name, id);
  }

  // ==================== Private Helpers ====================

  private async Task<OrganType> GetRequiredForWriteAsync(IUnitOfWork _uow, int id, CancellationToken ct)
  {
    var organType = await _uow.BasicRepository<OrganType>().GetByIdAsync(new object[] { id }, ct);

    if (organType == null)
    {
      _logger.LogWarning("Попытка выполнения операции над несуществующим типом органа (ID: {OrganTypeId})", id);
      throw new KeyNotFoundException($"Тип органа с ID '{id}' не найден.");
    }

    return organType;
  }

  public Task<int> CreateFromDtoAsync(CreateOrganTypeDto input, CancellationToken ct = default) {
    ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(input.Fields);
    var entity = new OrganType(); input.Fields.ApplyTo(entity);
    return CreateAsync(entity, ct);
  }
  public async Task UpdateFromDtoAsync(EditOrganTypeDto input, CancellationToken ct = default) {
    ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(input.Fields);
    var entity = new OrganType { Id = input.Id };
    input.Fields.ApplyTo(entity); entity.Version = input.Version;
    await UpdateAsync(entity, ct);
  }
}
