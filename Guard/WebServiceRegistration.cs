using Guard.Components.Account;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;

namespace Guard;

public static class WebServiceRegistration
{
  public static IServiceCollection AddGuardWeb(this IServiceCollection services, IConfiguration configuration)
  {
    // Заголовки принимает только от явно настроенных доверенных прокси (по умолчанию — loopback).
    services.Configure<ForwardedHeadersOptions>(options =>
    {
      options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
      foreach (var value in configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
      {
        if (!System.Net.IPAddress.TryParse(value, out var proxy))
          throw new InvalidOperationException("ReverseProxy:KnownProxies содержит некорректный IP-адрес.");
        options.KnownProxies.Add(proxy);
      }
    });

    services.AddCascadingAuthenticationState();
    services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider<ApplicationUser>>();
    services.AddScoped<IdentityRedirectManager>();
    services.AddScoped<ICurrentSession, CurrentSessionAdapter>();
    services.AddScoped<AccessRefreshCoordinator>();
    services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, AccessCircuitHandler>();
    services.AddAuthorization(options =>
    {
      foreach (var permission in PermissionCatalog.All)
        options.AddPolicy(permission.Code, policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission.Code)));
    });
    services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionAuthorizationHandler>();

    return services;
  }
}
