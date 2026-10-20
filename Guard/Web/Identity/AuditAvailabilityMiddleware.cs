using Guard.Core.Services;

namespace Guard.Core.Identity;

public sealed class AuditAvailabilityMiddleware(RequestDelegate next)
{
  public async Task InvokeAsync(HttpContext context, IOperationAvailability availability)
  {
    var path = context.Request.Path;
    if (context.Items.ContainsKey(AuditCookieValidation.IdentityUnavailable) &&
      !path.Equals("/Account/Logout", StringComparison.OrdinalIgnoreCase) &&
      !path.Equals("/Account/AuditUnavailable", StringComparison.OrdinalIgnoreCase)) {
      await UnavailableAsync(context); return;
    }
    var exception = path.StartsWithSegments("/Account/Login", StringComparison.OrdinalIgnoreCase) ||
      path.StartsWithSegments("/Account/Logout", StringComparison.OrdinalIgnoreCase) ||
      path.StartsWithSegments("/Account/Audit", StringComparison.OrdinalIgnoreCase) ||
      path.StartsWithSegments("/Account/AuditUnavailable", StringComparison.OrdinalIgnoreCase) ||
      path.StartsWithSegments("/audit/settings", StringComparison.OrdinalIgnoreCase) ||
      path.StartsWithSegments("/audit/diagnostics", StringComparison.OrdinalIgnoreCase) ||
      path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase);
    if (exception || context.User.Identity?.IsAuthenticated != true) { await next(context); return; }
    try {
      if (context.User.HasClaim("guard:audit-recovery", "1"))
        throw new AuditUnavailableException("RecoveryOnly", "Вход разрешает только диагностику аудита.");
      await availability.RequireWorkingAsync(context.RequestAborted);
      await next(context);
    }
    catch (AuditUnavailableException) when (!context.Response.HasStarted) {
      await UnavailableAsync(context);
    }
  }
  private static async Task UnavailableAsync(HttpContext context)
  {
      if (context.Request.Headers.Accept.Any(a => a?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true))
        context.Response.Redirect("/Account/AuditUnavailable");
      else {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = "5";
        await context.Response.WriteAsync("Обязательный аудит недоступен. Рабочие операции приостановлены.", context.RequestAborted);
      }
  }
}
