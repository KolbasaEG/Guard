using Guard.Core.Services;
using System.Security.Claims;

namespace Guard.Core.Identity
{
  public class IpCheckMiddleware
  {
    private readonly RequestDelegate _next;

    public IpCheckMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ISecurityService securityService)
    {
      if (context.User.Identity?.IsAuthenticated == true)
      {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var remoteIp = context.Connection.RemoteIpAddress?.ToString();

        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(remoteIp))
        {
          //var isAllowed = await securityService.IsIpAllowedAsync(userId, remoteIp);
          //if (!isAllowed)
          //{
          //  context.Response.StatusCode = StatusCodes.Status403Forbidden;
          //  await context.Response.WriteAsync("Доступ с текущего IP-адреса запрещен.");
          //  return;
          //}
        }
      }
      await _next(context);
    }
  }
}
