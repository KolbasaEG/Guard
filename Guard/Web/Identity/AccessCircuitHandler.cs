using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Components.Authorization;
using Guard.Core.Services;
using System.Security.Claims;
namespace Guard.Core.Identity;
public sealed class AccessCircuitHandler(AccessRefreshCoordinator coordinator, AuthenticationStateProvider authentication,
    IPermissionService permissions) : CircuitHandler
{
  public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken) => coordinator.RequestAsync();
  public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next) =>
    async context => {
      var user = (await authentication.GetAuthenticationStateAsync()).User;
      var id = user.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
      if (id != null) {
        try { await permissions.GetCurrentAsync(); }
        catch (UnauthorizedAccessException) { await coordinator.RequestAsync(); return; }
      }
      await next(context);
    };
}
