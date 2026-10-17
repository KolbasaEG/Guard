namespace Guard.Core.Services;

public class DataAccessScopeService(IPermissionService permissions, IDataAccessScopeReader reader)
    : IDataAccessScopeService
{
  public async Task<DataAccessScope> GetAsync(CancellationToken ct = default)
  {
    var user = await permissions.GetCurrentAsync(ct);
    if (user.IsRoot) return new(user.UserId, true, []);
    var path = await reader.FindUserPathAsync(user.UserId, ct);
    var ids = !SubdivisionAccessPath.IsValid(path) ? Array.Empty<Guid>() : await reader.GetSubdivisionIdsAsync(path!, ct);
    return new(user.UserId, false, ids.ToHashSet());
  }
}
