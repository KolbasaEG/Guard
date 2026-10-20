using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class MiddlewareTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    await CheckAuditGateAsync(check);
    foreach (var (authenticated, allowed, logout, expected) in new[] {
        (false, false, false, true), (true, true, false, true),
        (true, false, false, false), (true, false, true, true) })
    {
      var context = Context(authenticated);
      context.Request.Path = logout ? "/Account/Logout" : "/_blazor";
      var reached = false;
      var middleware = new IpCheckMiddleware(_ => { reached = true; return Task.CompletedTask; });
      await middleware.InvokeAsync(context, new AccessStub(allowed), new AuditStub());
      check(reached == expected, $"middleware authenticated={authenticated} allowed={allowed} logout={logout}");
      if (!expected) check(context.Response.StatusCode == 403, "denied connection returns 403");
    }
    var missingId = Context(true);
    missingId.User = new ClaimsPrincipal(new ClaimsIdentity([], "test"));
    await new IpCheckMiddleware(_ => throw new Exception("missing id accepted"))
        .InvokeAsync(missingId, new AccessStub(true), new AuditStub());
    check(missingId.Response.StatusCode == 403, "authenticated principal without user ID denied");
    var spoof = Context(false);
    spoof.Request.Headers["X-Forwarded-For"] = "203.0.113.1";
    check(ClientIpAddress.Get(spoof) == "192.168.1.1", "raw forwarded header is not trusted");
    var exceptionContext = Context(false);
    await new IpCheckMiddleware(_ => throw new IpAccessDeniedException())
        .InvokeAsync(exceptionContext, new AccessStub(false), new AuditStub());
    check(exceptionContext.Response.StatusCode == 403, "direct sign-in denial handled");
    foreach (var (remote, expected) in new[] { ("203.0.113.10", "203.0.113.10"), ("127.0.0.1", "192.168.1.1") })
    {
      var forwarded = Context(false);
      forwarded.Connection.RemoteIpAddress = IPAddress.Parse(remote);
      forwarded.Request.Headers["X-Forwarded-For"] = "192.168.1.1";
      var options = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor };
      await new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options))
          .Invoke(forwarded);
      check(ClientIpAddress.Get(forwarded) == expected, $"trusted proxy handling {remote}");
    }
  }

  private static async Task CheckAuditGateAsync(Action<bool, string> check)
  {
    var validationHttp = Context(true);
    var cookie = new Microsoft.AspNetCore.Authentication.Cookies.CookieValidatePrincipalContext(validationHttp,
      new Microsoft.AspNetCore.Authentication.AuthenticationScheme("test", "test", typeof(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationHandler)),
      new Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions(),
      new Microsoft.AspNetCore.Authentication.AuthenticationTicket(validationHttp.User, "test"));
    await AuditCookieValidation.ValidateAsync(cookie, _ => Task.FromException(new Npgsql.PostgresException("synthetic", "ERROR", "ERROR", "08006")));
    check(validationHttp.Items.ContainsKey(AuditCookieValidation.IdentityUnavailable) && !cookie.ShouldRenew,
      "failed Identity validation marks request as blocked without extending cookie");
    foreach (var path in new[] { "/audit/settings", "/audit/diagnostics", "/_blazor", "/Account/Login", "/Account/Logout/extra" }) {
      var ctx = Context(true); ctx.Request.Path = path; ctx.Items[AuditCookieValidation.IdentityUnavailable] = true;
      await new AuditAvailabilityMiddleware(_ => throw new Exception("unverified Identity bypass")).InvokeAsync(ctx, new Availability(true));
      check(ctx.Response.StatusCode == 503, "unavailable Identity blocks even recovery endpoint " + path);
    }
    foreach (var path in new[] { "/Account/Logout", "/Account/AuditUnavailable" }) {
      var ctx = Context(true); ctx.Request.Path = path; ctx.Items[AuditCookieValidation.IdentityUnavailable] = true;
      var reached = false;
      await new AuditAvailabilityMiddleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(ctx, new Availability(false));
      check(reached, "unavailable Identity retains exact public refusal/logout path " + path);
    }
    foreach (var path in new[] { "/", "/personals", "/exports/123/download", "/Account/Manage/ChangePassword" }) {
      var ctx = Context(true); ctx.Request.Path = path;
      var reached = false;
      await new AuditAvailabilityMiddleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(ctx, new Availability(false));
      check(!reached && ctx.Response.StatusCode == 503 && ctx.Response.Headers.RetryAfter == "5", "unhealthy audit blocks ordinary HTTP operation " + path);
    }
    foreach (var path in new[] { "/Account/Logout", "/Account/Audit", "/Account/AuditUnavailable", "/audit/settings", "/audit/diagnostics", "/_blazor" }) {
      var ctx = Context(true); ctx.Request.Path = path;
      var reached = false;
      await new AuditAvailabilityMiddleware(_ => { reached = true; return Task.CompletedTask; }).InvokeAsync(ctx, new Availability(false));
      check(reached, "recovery transport and limited endpoints remain reachable " + path);
    }
    var html = Context(true); html.Request.Path = "/personals"; html.Request.Headers.Accept = "text/html";
    await new AuditAvailabilityMiddleware(_ => throw new Exception("HTML bypass")).InvokeAsync(html, new Availability(false));
    check(html.Response.StatusCode == 302 && html.Response.Headers.Location == "/Account/AuditUnavailable", "ordinary HTML redirects to safe outage page");
    var recovered = Context(true); recovered.Request.Path = "/personals";
    ((ClaimsIdentity)recovered.User.Identity!).AddClaim(new("guard:audit-recovery", "1"));
    await new AuditAvailabilityMiddleware(_ => throw new Exception("recovery cookie bypass")).InvokeAsync(recovered, new Availability(true));
    check(recovered.Response.StatusCode == 503, "recovery-only cookie cannot become ordinary access after repair");
    var ready = Context(true); ready.Request.Path = "/personals"; var resumed = false;
    await new AuditAvailabilityMiddleware(_ => { resumed = true; return Task.CompletedTask; }).InvokeAsync(ready, new Availability(true));
    check(resumed, "healthy audit automatically resumes ordinary session");
  }
  private sealed class Availability(bool ready) : IOperationAvailability
  {
    public Task RequireWorkingAsync(CancellationToken ct = default) => ready ? Task.CompletedTask : Task.FromException(new AuditUnavailableException("StorageUnavailable", "synthetic"));
  }

  internal static DefaultHttpContext Context(bool authenticated)
  {
    var context = new DefaultHttpContext();
    context.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:192.168.1.1");
    context.Response.Body = new MemoryStream();
    if (authenticated) context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user")], "test"));
    return context;
  }

  private sealed class AccessStub(bool allowed) : IIpAccessService
  {
    public Task<bool> IsAllowedAsync(string userId, string? ip, CancellationToken ct = default) => Task.FromResult(allowed);
  }
}

internal sealed class AuditStub : IAuditService
{
  public Task RecordAsync(Guard.Core.Services.AuditEventData entry, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); LogIdentityEvent(entry.EventType, entry.Actor, entry.ClientIp, entry.Details); return Task.CompletedTask; }
    public void LogIdentityEvent(AuditEventType type, string? name, string? ip = null, string? details = null) { }
}
