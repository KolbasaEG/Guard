using Guard.Core.Identity;
namespace Guard.Core.Services;
public class IpManagementAccessService(IPermissionService permissions, IDataAccessScopeService scopes) : IIpManagementAccessService
{
  public async Task<IpManagementScope> GetScopeAsync(bool write, CancellationToken ct = default)
  {
    await permissions.RequireAsync(write ? Permissions.IpAddresses.Manage : Permissions.IpAddresses.Read, ct);
    var scope = await scopes.GetAsync(ct);
    return new(scope.UserId, scope.IsRoot, scope.SubdivisionIds);
  }
}
