using Guard.Infrastructure;

namespace Guard;

// Registration only: no database initialization, migrations, seeding or host startup.
public static class GuardServiceRegistration
{
  public static IServiceCollection AddGuardServices(this IServiceCollection services, IConfiguration configuration, bool development)
  {
    services.AddGuardInfrastructure(configuration, development);
    services.AddGuardApplication();
    services.AddGuardWeb(configuration);
    return services;
  }
}
