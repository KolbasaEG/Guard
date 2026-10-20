using System.Data.Common;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Guard.Core.Identity;

public static class AuditCookieValidation
{
  public const string IdentityUnavailable = "Guard.IdentityUnavailable";
  private const string DeferredRefresh = "Guard.DeferredCookieRefresh";
  public static async Task DeferRefreshAsync(CookieSlidingExpirationContext context, Func<CookieSlidingExpirationContext, Task> check)
  {
    await check(context);
    context.HttpContext.Items[DeferredRefresh] = context.ShouldRenew;
    context.ShouldRenew = false;
  }
  public static async Task ValidateAsync(CookieValidatePrincipalContext context, Func<CookieValidatePrincipalContext, Task> validate)
  {
    try {
      await validate(context);
      if (context.Principal != null && context.HttpContext.Items.Remove(DeferredRefresh, out var refresh) && refresh is true)
        context.ShouldRenew = true;
    }
    catch (Exception error) when (error is DbException or TimeoutException) {
      // Криптографически проверенная cookie сохраняет идентичность только для CSRF-защищённого выхода.
      // Никакой рабочий endpoint или диагностика Root не должны использовать её без доступной Identity-БД.
      context.ShouldRenew = false;
      context.HttpContext.Items[IdentityUnavailable] = true;
      new Guard.Core.Logging.InternalLogWriter().Write($"IdentityValidationUnavailable ErrorType={error.GetType().Name}");
    }
  }
}
