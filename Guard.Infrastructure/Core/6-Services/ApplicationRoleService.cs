using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Guard.Core.Services.DTOs;
using Guard.Core.Identity;

namespace Guard.Core.Services;

public class ApplicationRoleService : IRoleSearchService
{
  private readonly IReadRepository<ApplicationRole> _readRoleRepository;
  private readonly IPermissionService _permissions;

  public ApplicationRoleService(
      IReadRepository<ApplicationRole> readRoleRepository,
      IPermissionService permissions)
  {
    _readRoleRepository = readRoleRepository;
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
}
