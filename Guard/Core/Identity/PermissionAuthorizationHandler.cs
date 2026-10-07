using Guard.Core.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Guard.Core.Identity;

public record PermissionRequirement(string Permission) : IAuthorizationRequirement;
public class PermissionAuthorizationHandler(IPermissionService permissions, IAuditService audit) : AuthorizationHandler<PermissionRequirement>
{
  protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
  {
    var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (context.User.Identity?.IsAuthenticated != true || id == null) return;
    try
    {
      if ((await permissions.GetForUserAsync(id)).Has(requirement.Permission)) context.Succeed(requirement);
      else audit.LogIdentityEvent(Guard.Core.Enums.AuditEventType.AccessDenied, id, details: $"Route permission={requirement.Permission}");
    }
    catch (UnauthorizedAccessException) { }
  }
}
