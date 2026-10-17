using Guard.Core.Services;

namespace Guard;

// Composition belongs to the host; Guard.Application stays independent of DI packages.
public static class ApplicationServiceRegistration
{
  public static IServiceCollection AddGuardApplication(this IServiceCollection services)
  {
    services.AddScoped<IDataAccessScopeService, DataAccessScopeService>();
    services.AddScoped<IIpAccessService, IpAccessService>();
    services.AddScoped<IPermissionService, PermissionService>();
    services.AddScoped<IIpManagementAccessService, IpManagementAccessService>();
    return services;
  }
}
