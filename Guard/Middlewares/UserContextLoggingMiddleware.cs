namespace Guard.Middlewares;

using System.Security.Claims;
using Serilog.Context;

public class UserContextLoggingMiddleware
{
  private readonly RequestDelegate _next;

  public UserContextLoggingMiddleware(RequestDelegate next)
  {
    _next = next;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    context.Response.OnStarting(() => { context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier; return Task.CompletedTask; });
    // 1. Получаем IP-адрес клиента (с учетом работы за прокси Nginx / IIS)
    var ipAddress = Guard.Core.Identity.ClientIpAddress.Get(context) ?? "unknown";

    // 2. Получаем ID текущего пользователя из Claim (ASP.NET Core Identity)
    var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? context.User?.Identity?.Name
                 ?? "anonymous";

    // 3. Помещаем контекст в Serilog.
    // Переменные "ClientIp" и "UserId" автоматически попадут в одноименные столбцы БД PostgreSQL.
    using (LogContext.PushProperty("ClientIp", ipAddress))
    using (LogContext.PushProperty("UserId", userId))
    using (LogContext.PushProperty("CorrelationId", context.TraceIdentifier))
    using (Guard.Core.Services.AuditOperationContext.Push(new(userId == "anonymous" ? null : userId,
      ipAddress == "unknown" ? null : ipAddress, context.TraceIdentifier, userId == "anonymous" ? "anonymous" : "user")))
    {
      // Передаем управление следующему компоненту в пайплайне
      await _next(context);
    }
  }
}

// Удобный метод-расширение для красивого подключения в Program.cs
public static class UserContextLoggingMiddlewareExtensions
{
  public static IApplicationBuilder UseUserContextLogging(this IApplicationBuilder builder)
  {
    return builder.UseMiddleware<UserContextLoggingMiddleware>();
  }
}
