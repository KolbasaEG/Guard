using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Components.Authorization;
using Guard.Core.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
namespace Guard.Core.Identity;
public sealed class AccessCircuitHandler(AccessRefreshCoordinator coordinator, AuthenticationStateProvider authentication,
    IPermissionService permissions, IClientIpAccessor clientIp, NavigationManager? navigation = null,
    IAuditAdministrationService? administration = null) : CircuitHandler
{
  public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken) => coordinator.RequestAsync();
  public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next) =>
    async context => {
      var user = (await authentication.GetAuthenticationStateAsync()).User;
      var id = user.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
      var correlation = Guid.NewGuid().ToString("N");
      var ip = clientIp.GetClientIp();
      using var metadata = AuditOperationContext.Push(new(id, ip, correlation, id == null ? "anonymous" : "user"));
      if (id != null) {
        try {
          var path = navigation == null ? null : new Uri(navigation.Uri).AbsolutePath.TrimEnd('/');
          if (administration != null && (string.Equals(path, "/audit/settings", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(path, "/audit/diagnostics", StringComparison.OrdinalIgnoreCase))) await administration.RequireRootAsync();
          else await permissions.GetCurrentAsync();
        }
        catch (UnauthorizedAccessException) { await coordinator.RequestAsync(); return; }
        catch (AuditUnavailableException) { await coordinator.RequestAsync(); }
      }
      using var actorLog = Serilog.Context.LogContext.PushProperty("UserId", id ?? "anonymous");
      using var ipLog = Serilog.Context.LogContext.PushProperty("ClientIp", ip ?? "unknown");
      using var correlationLog = Serilog.Context.LogContext.PushProperty("CorrelationId", correlation);
      await next(context);
    };
}
