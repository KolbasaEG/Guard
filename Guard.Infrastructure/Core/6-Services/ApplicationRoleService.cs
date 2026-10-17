using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Guard.Core.Services.DTOs;
using Guard.Core.Identity;

namespace Guard.Core.Services;

public class ApplicationRoleService : IApplicationRoleService
{
  private readonly IReadRepository<ApplicationRole> _readRoleRepository;
  private readonly ILogger<ApplicationRoleService> _logger;
  private readonly IRoleAccessService _access;
  private readonly IPermissionService _permissions;

  public ApplicationRoleService(
      IReadRepository<ApplicationRole> readRoleRepository,
      ILogger<ApplicationRoleService> logger, IRoleAccessService access, IPermissionService permissions)
  {
    _readRoleRepository = readRoleRepository;
    _logger = logger;
    _access = access;
    _permissions = permissions;
  }

  public async Task<RoleSearchResult> SearchAsync(RoleSearchRequest request, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    var actor = await SearchAccess.RequireAsync(_permissions, Permissions.Roles.Read, ct);
    if (request.Skip < 0 || request.Take is < 1 or > 100) throw new ArgumentException("Недопустимые параметры страницы.");
    RoleSearchQuery.ValidateFilter(request.Filter);
    var result = await _readRoleRepository.QueryAsync(async query => {
      query = RoleSearchQuery.Filter(query, request.Filter);
      var count = await query.CountAsync(ct);
      var items = await RoleSearchQuery.Sort(query, request.OrderBy).Skip(request.Skip).Take(request.Take)
        .Select(r => new RoleListDto(r.Id, r.Name, r.NormalizedName)).ToListAsync(ct);
      return new RoleSearchResult(items, count);
    }, ct);
    await SearchAccess.RecheckAsync(_permissions, Permissions.Roles.Read, actor, ct);
    return result;
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
    return await _access.SaveAsync(new Guard.Core.Services.DTOs.RoleEditDto { Name = role.Name ?? "" }, ct);
  }

  public async Task UpdateAsync(ApplicationRole role, CancellationToken ct = default)
  {
    var input = await _access.GetAsync(role.Id, ct);
    input.Name = role.Name ?? "";
    input.Version = role.ConcurrencyStamp;
    await _access.SaveAsync(input, ct);
  }

  public async Task DeleteAsync(string id, CancellationToken ct = default)
  {
    await _access.DeleteAsync(id, ct);
  }

}
