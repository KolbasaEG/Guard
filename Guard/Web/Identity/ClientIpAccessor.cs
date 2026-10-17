using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.AspNetCore.Http;

namespace Guard.Web.Identity;

public sealed class ClientIpAccessor(IHttpContextAccessor http) : IClientIpAccessor
{
  public string? GetClientIp() => http.HttpContext is { } context ? ClientIpAddress.Get(context) : null;
}
