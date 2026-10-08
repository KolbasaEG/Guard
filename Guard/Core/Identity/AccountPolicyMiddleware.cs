using System.Security.Claims;
using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Guard.Core.Identity;

public sealed class AccountPolicyMiddleware(RequestDelegate next)
{
  public async Task InvokeAsync(HttpContext context, IAccountPolicyService policies, UserManager<ApplicationUser> manager)
  {
    if (context.User.Identity?.IsAuthenticated == true && !context.Request.Path.StartsWithSegments("/Account/Logout"))
    {
      var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
      var user = id == null ? null : await manager.FindByIdAsync(id);
      var stamp = context.User.FindFirstValue(manager.Options.ClaimsIdentity.SecurityStampClaimType);
      var state = user == null ? new AccountState(true, false) : await policies.CheckAsync(user.Id, context.RequestAborted);
      if (state.Blocked || user?.SecurityStamp != stamp) {
        await context.SignOutAsync(IdentityConstants.ApplicationScheme);
        context.Response.Redirect("/Account/Login");
        return;
      }
      var passwordPage = context.Request.Path.Equals("/Account/Manage/ChangePassword", StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.Equals("/Account/Manage/SetPassword", StringComparison.OrdinalIgnoreCase);
      if (state.PasswordExpired && !passwordPage) {
        context.Response.Redirect("/Account/Manage/ChangePassword");
        return;
      }
      // HTTP-навигация и отправка формы; транспорт и служебные запросы не считаются активностью.
      if (context.Request.Headers.Accept.Any(v => v?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true))
        await policies.RecordActivityAsync(user!.Id, context.RequestAborted);
    }
    await next(context);
  }
}
