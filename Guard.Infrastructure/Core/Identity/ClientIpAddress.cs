namespace Guard.Core.Identity;

public static class ClientIpAddress
{
  public static string? Get(HttpContext context)
  {
    var address = context.Connection.RemoteIpAddress;
    if (address?.IsIPv4MappedToIPv6 == true) address = address.MapToIPv4();
    return address?.ToString();
  }
}
