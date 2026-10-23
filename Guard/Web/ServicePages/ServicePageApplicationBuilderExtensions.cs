using Microsoft.AspNetCore.Diagnostics;

namespace Guard.Web.ServicePages;

public static class ServicePageApplicationBuilderExtensions
{
  // До UseRouting: повторное выполнение должно найти endpoint /not-found.
  public static IApplicationBuilder UseGuardNotFoundPage(this IApplicationBuilder app)
  {
    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    return app.Use(async (context, next) => {
      await next(context);
      var acceptsHtml = context.Request.GetTypedHeaders().Accept?.Any(value =>
        value.MediaType.Value?.Equals("text/html", StringComparison.OrdinalIgnoreCase) == true && value.Quality != 0) == true;
      // Только неизвестная HTML-страница. API, ресурсы и прочие ошибки сохраняют свой ответ.
      if (context.Response.StatusCode != StatusCodes.Status404NotFound || context.GetEndpoint() != null ||
          !HttpMethods.IsGet(context.Request.Method) ||
          !acceptsHtml) {
        if (context.Features.Get<IStatusCodePagesFeature>() is { } pages) pages.Enabled = false;
      }
    });
  }
}
