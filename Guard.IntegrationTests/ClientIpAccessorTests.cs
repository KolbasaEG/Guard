using System.Net;
using Guard.Web.Identity;
using Microsoft.AspNetCore.Http;

internal static class ClientIpAccessorTests
{
  public static void Run(Action<bool, string> check)
  {
    var http = new HttpContextAccessor();
    var accessor = new ClientIpAccessor(http);
    check(accessor.GetClientIp() == null, "client IP is absent outside HTTP");
    var context = new DefaultHttpContext();
    http.HttpContext = context;
    context.Request.Headers["X-Forwarded-For"] = "203.0.113.99";
    context.Request.Headers["Forwarded"] = "for=203.0.113.98";
    check(accessor.GetClientIp() == null, "raw forwarding headers cannot supply client metadata");
    foreach (var (input, expected) in new[] { ("198.18.200.1", "198.18.200.1"),
      ("::ffff:198.18.200.1", "198.18.200.1"), ("2001:db8::1", "2001:db8::1") }) {
      context.Connection.RemoteIpAddress = IPAddress.Parse(input);
      check(accessor.GetClientIp() == expected, "client metadata reads current trusted remote address and normalizes mapped IPv4");
    }
    http.HttpContext = null;
    check(accessor.GetClientIp() == null, "client IP adapter does not retain previous request metadata");
  }
}
