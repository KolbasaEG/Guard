using Microsoft.AspNetCore.Components.Server.Circuits;
namespace Guard.Core.Identity;
public sealed class AccessCircuitHandler(AccessRefreshCoordinator coordinator) : CircuitHandler
{
  public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken) => coordinator.RequestAsync();
}
