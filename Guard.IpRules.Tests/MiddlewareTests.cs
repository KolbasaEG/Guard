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
  public void LogIdentityEvent(AuditEventType type, string? name, string? ip = null, string? details = null) { }
}
