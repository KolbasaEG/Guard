using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Routing
{
  internal static class IdentityComponentsEndpointRouteBuilderExtensions
  {
    // These endpoints are required by the Identity Razor components defined in the /Components/Account/Pages directory of this project.
    public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
      ArgumentNullException.ThrowIfNull(endpoints);

      var accountGroup = endpoints.MapGroup("/Account");

      accountGroup.MapPost("/ChangeOwnPassword", async (
          HttpContext context,
          [FromServices] IOwnPasswordService passwords,
          [FromServices] IAntiforgery antiforgery) =>
      {
        await antiforgery.ValidateRequestAsync(context);
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var result = await passwords.ChangeAsync(form["OldPassword"].ToString(),
          form["NewPassword"].ToString(), form["Confirmation"].ToString(), context.RequestAborted);
        return result.Status switch {
          OwnPasswordChangeStatus.Succeeded => Results.Ok(),
          OwnPasswordChangeStatus.Unauthorized => Results.Unauthorized(),
          _ => Results.BadRequest(new { error = result.Error })
        };
      }).RequireAuthorization();

      accountGroup.MapPost("/Logout", async (
        HttpContext context,
        [FromServices] ILocalAccountService accounts,
        [FromServices] IAntiforgery antiforgery,
        [FromForm] string? returnUrl) =>
      {
        await antiforgery.ValidateRequestAsync(context);
        await accounts.SignOutAsync(context.RequestAborted);
        return TypedResults.LocalRedirect(Guard.Components.Account.AccountReturnUrl.Normalize(returnUrl));
      });

      return accountGroup;
    }
  }
}
