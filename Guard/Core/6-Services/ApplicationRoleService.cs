using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public class ApplicationRoleService : IApplicationRoleService
{
  private readonly IReadRepository<ApplicationRole> _readRoleRepository;
  private readonly IUnitOfWork _uow;
  private readonly ILogger<ApplicationRoleService> _logger;

  public ApplicationRoleService(
      IReadRepository<ApplicationRole> readRoleRepository,
      IUnitOfWork unitOfWork,
      ILogger<ApplicationRoleService> logger)
  {
    _readRoleRepository = readRoleRepository;
    _uow = unitOfWork;
    _logger = logger;
  }

  // ==================== Read (Изолированный IReadRepository) ====================

  public async Task<TResult> QueryRolesAsync<TResult>(
      Func<IQueryable<ApplicationRole>, Task<TResult>> query,
      CancellationToken ct = default)
  {
    return await _readRoleRepository.QueryAsync(query, ct);
  }

  public async Task<ApplicationRole?> GetByIdAsync(string id, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос роли по ID: {RoleId}", id);
    return await _readRoleRepository.QueryAsync(query =>
        query.FirstOrDefaultAsync(r => r.Id == id, ct), ct);
  }

  public async Task<ApplicationRole?> GetByNameAsync(string roleName, CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(roleName))
      return null;

    var normalizedName = roleName.Trim().ToUpperInvariant();

    _logger.LogDebug("Запрос роли по имени: {RoleName}", roleName);
    return await _readRoleRepository.QueryAsync(query =>
        query.FirstOrDefaultAsync(r => r.NormalizedName == normalizedName, ct), ct);
  }

  public async Task<IReadOnlyList<ApplicationRole>> GetAllAsync(CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос всех ролей");
    return await _readRoleRepository.QueryAsync(query =>
        query.OrderBy(r => r.Name)
             .ToListAsync(ct),
        ct);
  }

  public async Task<bool> IsRoleNameUniqueAsync(string roleName, string? excludeId = null, CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(roleName))
      return true;

    var normalizedName = roleName.Trim().ToUpperInvariant();

    return await _readRoleRepository.QueryAsync(query =>
        query.AllAsync(r => r.NormalizedName != normalizedName || (excludeId != null && r.Id == excludeId), ct),
        ct);
  }

  // ==================== Write (IUnitOfWork & BasicRepository) ====================

  public async Task<string> CreateAsync(ApplicationRole role, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(role);

    return await _uow.ExecuteInTransactionAsync(async () =>
    {
      if (string.IsNullOrWhiteSpace(role.Id))
      {
        role.Id = Guid.NewGuid().ToString();
      }

      if (!string.IsNullOrWhiteSpace(role.Name))
      {
        role.Name = role.Name.Trim();
        role.NormalizedName = role.Name.ToUpperInvariant();
      }

      // Так как ApplicationRole не наследуется от BaseEntity, используем BasicRepository
      await _uow.BasicRepository<ApplicationRole>().AddAsync(role, ct);
      await _uow.SaveChangesAsync(ct);

      _logger.LogInformation("Создана новая роль '{RoleName}' (ID: {RoleId})", role.Name, role.Id);

      return role.Id;
    }, ct);
  }

  public async Task UpdateAsync(ApplicationRole role, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(role);

    if (!string.IsNullOrWhiteSpace(role.Name))
    {
      role.Name = role.Name.Trim();
      role.NormalizedName = role.Name.ToUpperInvariant();
    }

    await _uow.BasicRepository<ApplicationRole>().UpdateAsync(role, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Обновлены данные роли '{RoleName}' (ID: {RoleId})", role.Name, role.Id);
  }

  public async Task DeleteAsync(string id, CancellationToken ct = default)
  {
    var role = await GetRequiredForWriteAsync(id, ct);

    await _uow.BasicRepository<ApplicationRole>().DeleteAsync(role, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("Роль '{RoleName}' (ID: {RoleId}) была удалена из системы", role.Name, id);
  }

  // ==================== Private Helpers ====================

  private async Task<ApplicationRole> GetRequiredForWriteAsync(string id, CancellationToken ct)
  {
    var role = await _uow.BasicRepository<ApplicationRole>().GetByIdAsync([id], ct);

    if (role == null)
    {
      _logger.LogWarning("Попытка выполнения операции над несуществующей ролью (ID: {RoleId})", id);
      throw new KeyNotFoundException($"Роль с ID '{id}' не найдена.");
    }

    return role;
  }
}