using System.Net;
using System.Security.Claims;
using Guard.Core.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class AuditCookieHandlerTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var failed = true;
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ContentRootPath = Path.GetTempPath() });
    builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
    builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
    builder.Services.AddAuthentication("test").AddCookie("test", options => {
      options.SlidingExpiration = true; options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
      var sliding = options.Events.OnCheckSlidingExpiration;
      options.Events.OnCheckSlidingExpiration = context => AuditCookieValidation.DeferRefreshAsync(context, sliding);
      options.Events.OnValidatePrincipal = context => AuditCookieValidation.ValidateAsync(context, _ =>
        failed ? Task.FromException(new Npgsql.PostgresException("synthetic", "ERROR", "ERROR", "08006")) : Task.CompletedTask);
    });
    await using var app = builder.Build(); app.UseAuthentication();
    app.MapGet("/issue", async (HttpContext ctx) => {
      await ctx.SignInAsync("test", new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "fixture")], "test")),
        new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow.AddMinutes(-8), ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(2), IsPersistent = true });
    });
    app.MapGet("/check", (HttpContext ctx) => {
      ctx.Response.StatusCode = ctx.Items.ContainsKey(AuditCookieValidation.IdentityUnavailable) ? 503 : ctx.User.Identity?.IsAuthenticated == true ? 200 : 401;
    });
    await app.StartAsync();
    try {
      using var client = new HttpClient(new HttpClientHandler { CookieContainer = new(), AllowAutoRedirect = false }) {
        BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single())
      };
      using var issue = await client.GetAsync("/issue");
      check(issue.IsSuccessStatusCode && issue.Headers.Contains("Set-Cookie"), "handler fixture issues an old valid signed cookie");
      using var denied = await client.GetAsync("/check");
      check(denied.StatusCode == HttpStatusCode.ServiceUnavailable && !denied.Headers.Contains("Set-Cookie"),
        "actual cookie handler does not perform scheduled sliding renewal after failed Identity validation");
      failed = false;
      using var healthy = await client.GetAsync("/check");
      check(healthy.IsSuccessStatusCode && healthy.Headers.Contains("Set-Cookie"),
        "actual cookie handler retains ordinary sliding renewal after successful validation");
    } finally { await app.StopAsync(); }
  }
}
