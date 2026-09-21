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
    // 1. Получаем IP-адрес клиента (с учетом работы за прокси Nginx / IIS)
    var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";

    // 2. Получаем ID текущего пользователя из Claim (ASP.NET Core Identity)
    var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? context.User?.Identity?.Name
                 ?? "anonymous";

    // 3. Помещаем контекст в Serilog.
    // Переменные "ClientIp" и "UserId" автоматически попадут в одноименные столбцы БД PostgreSQL.
    using (LogContext.PushProperty("ClientIp", ipAddress))
    using (LogContext.PushProperty("UserId", userId))
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