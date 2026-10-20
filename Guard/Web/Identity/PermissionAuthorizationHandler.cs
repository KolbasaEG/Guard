using Guard.Core.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Guard.Core.Identity;

public record PermissionRequirement(string Permission) : IAuthorizationRequirement;
public class PermissionAuthorizationHandler(IPermissionService permissions, IAuditService audit,
  IOperationAvailability? availability = null) : AuthorizationHandler<PermissionRequirement>
{
  protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
  {
    var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (context.User.Identity?.IsAuthenticated != true || id == null) return;
    try
    {
      if (context.User.HasClaim("guard:audit-recovery", "1")) { context.Fail(); return; }
      if (availability != null) await availability.RequireWorkingAsync();
      if ((await permissions.GetForUserAsync(id)).Has(requirement.Permission)) context.Succeed(requirement);
      else audit.LogIdentityEvent(Guard.Core.Enums.AuditEventType.AccessDenied, id, details: $"Route permission={requirement.Permission}");
    }
    catch (AuditUnavailableException) { context.Fail(); }
    catch (UnauthorizedAccessException) {
      await audit.RecordAsync(AuditEventData.ForAccount(Guard.Core.Enums.AuditEventType.AccessDenied, id)
        with { ReasonCode = "AccountPolicyDenied", Outcome = "failure" });
    }
  }
}
