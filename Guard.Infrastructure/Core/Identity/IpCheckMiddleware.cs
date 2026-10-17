using Guard.Core.Services;
using System.Security.Claims;

namespace Guard.Core.Identity
{
  public class IpCheckMiddleware
  {
    private readonly RequestDelegate _next;

    public IpCheckMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IIpAccessService access, IAuditService audit)
    {
      if (context.User.Identity?.IsAuthenticated == true)
      {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var remoteIp = ClientIpAddress.Get(context);

        // Выход доступен даже после отзыва IP, чтобы пользователь мог сменить учётную запись.
        if (!context.Request.Path.StartsWithSegments("/Account/Logout") &&
            (string.IsNullOrEmpty(userId) || !await access.IsAllowedAsync(userId, remoteIp, context.RequestAborted)))
        {
          audit.LogIdentityEvent(Core.Enums.AuditEventType.LoginFailed, context.User.Identity?.Name, remoteIp, "IP connection denied");
          await DenyAsync(context);
          return;
        }
      }
      try
      {
        await _next(context);
      }
      catch (IpAccessDeniedException) when (!context.Response.HasStarted)
      {
        context.Response.Clear();
        await DenyAsync(context);
      }
    }

    private static Task DenyAsync(HttpContext context)
    {
      context.Response.StatusCode = StatusCodes.Status403Forbidden;
      context.Response.ContentType = "text/plain; charset=utf-8";
      return context.Response.WriteAsync("Доступ с текущего IP-адреса запрещён. Обратитесь к администратору.", context.RequestAborted);
    }
  }
}
