using System.Security.Claims;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace Guard.Components.Account;

/// <summary>Blazor circuit имеет приоритет; HTTP используется вне активного circuit.</summary>
public sealed class CurrentSessionAdapter(AuthenticationStateProvider authentication, IHttpContextAccessor http) : ICurrentSession
{
  public async Task<CurrentSession?> GetAsync(CancellationToken ct = default)
  {
    ct.ThrowIfCancellationRequested();
    ClaimsPrincipal? principal;
    try { principal = (await authentication.GetAuthenticationStateAsync().WaitAsync(ct)).User; }
    catch (InvalidOperationException) { principal = http.HttpContext?.User; }
    ct.ThrowIfCancellationRequested();
    var id = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
    return principal?.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(id)
      ? new(id, principal.FindFirstValue("AspNet.Identity.SecurityStamp")) : null;
  }
}
