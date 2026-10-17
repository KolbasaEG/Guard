
using Guard.Core.Identity;
namespace Guard.Core.Services.DTOs;
public record UserAccessSnapshot(string UserId, bool IsRoot, HashSet<string> Permissions)
{
  public bool Has(string permission)
  {
    var definition = PermissionCatalog.All.SingleOrDefault(p => p.Code == permission);
    if (definition == null) return false;
    if (IsRoot) return true;
    return !definition.RootOnly && Permissions.Contains(permission) &&
      (definition.Requires == null || Has(definition.Requires));
  }
}
