using Guard;
using Guard.Components.Account;
using Guard.Infrastructure;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Interceptors;
using Guard.Core.Logging;
using Guard.Core.Repositories;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;

internal static class ServiceRegistrationTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    foreach (var (development, separateComposition) in new[] { (false, false), (false, true), (true, false), (true, true) }) {
      // Empty host configuration: never reads working connections or secrets, builds or starts the app.
      var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions {
        EnvironmentName = development ? Environments.Development : Environments.Production,
        ApplicationName = typeof(GuardServiceRegistration).Assembly.GetName().Name
      });
      builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
        ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Port=1;Database=di_test;Username=unused;Timeout=1",
        ["ConnectionStrings:LogsConnection"] = "Host=127.0.0.1;Port=1;Database=di_logs;Username=unused;Timeout=1",
        ["DirectoryApi:PageSize"] = "1234",
        ["Exports:RowsPerPart"] = "200",
        ["ReverseProxy:KnownProxies:0"] = "127.0.0.2"
      });
      var services = builder.Services;
      services.AddLogging();
      using var manager = new DynamicLoggerManager(new LoggingLevelSwitch(), (_, _) => new LoggerConfiguration().CreateLogger());
      using var auditLogger = new LoggerConfiguration().CreateLogger();
      services.AddSingleton(manager);
      services.AddSingleton<IAuditService>(new AuditService(auditLogger));
      if (separateComposition) {
        // Register Web first: infrastructure must not rely on a UI registration order.
        services.AddGuardWeb(builder.Configuration);
        services.AddGuardApplication();
        services.AddGuardInfrastructure(builder.Configuration, development);
      }
      else services.AddGuardServices(builder.Configuration, development);

      var infrastructure = new ServiceCollection();
      infrastructure.AddGuardInfrastructure(builder.Configuration, development);
      check(!infrastructure.Any(d => d.ImplementationType == typeof(AccountPolicyWorker)),
        "Infrastructure leaves the account policy timer to the host");
      foreach (var worker in new[] { typeof(AccountPolicyWorker), typeof(ApiSynchronizationWorker), typeof(ExportWorker) })
        check(services.Count(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == worker) == 1 &&
          services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == worker).Lifetime == ServiceLifetime.Singleton,
          $"Composition registers exactly one singleton hosted worker {worker.Name}");
      check(!infrastructure.Any(d => d.ImplementationType == typeof(PermissionAuthorizationHandler)),
        "Infrastructure does not register the Web permission handler");
      check(services.Count(d => d.ImplementationType == typeof(PermissionAuthorizationHandler)) == 1 &&
        services.Single(d => d.ImplementationType == typeof(PermissionAuthorizationHandler)).Lifetime == ServiceLifetime.Scoped,
        "Web registers exactly one scoped permission handler in both composition orders");
      foreach (var webType in new[] { typeof(IIpManagementAccessService), typeof(ICurrentSession), typeof(IClientIpAccessor), typeof(AuthenticationStateProvider),
        typeof(IdentityRedirectManager), typeof(AccessRefreshCoordinator), typeof(CircuitHandler) })
        check(!infrastructure.Any(d => d.ServiceType == webType),
          $"infrastructure leaves Web adapter {webType.Name} to the host");
      check(!infrastructure.Any(d => d.ImplementationType?.Namespace?.StartsWith("Guard.Components", StringComparison.Ordinal) == true),
        "infrastructure descriptors do not register component implementations");
      var application = new ServiceCollection();
      application.AddGuardApplication();
      check(application.Count == 4 && application.All(d => d.Lifetime == ServiceLifetime.Scoped &&
        d.ServiceType.Assembly == typeof(ICurrentSession).Assembly &&
        d.ImplementationType?.Assembly == typeof(ICurrentSession).Assembly),
        "Application composition contains only four independent scoped decisions");
      check(services.Count(d => d.ServiceType == typeof(ICurrentSession)) == 1 &&
        services.Single(d => d.ServiceType == typeof(ICurrentSession)).ImplementationType == typeof(CurrentSessionAdapter) &&
        services.Single(d => d.ServiceType == typeof(ICurrentSession)).Lifetime == ServiceLifetime.Scoped,
        "host supplies one scoped Blazor/HTTP session adapter in both composition orders");
      check(services.Count(d => d.ServiceType == typeof(IClientIpAccessor)) == 1 &&
        services.Single(d => d.ServiceType == typeof(IClientIpAccessor)).ImplementationType == typeof(Guard.Web.Identity.ClientIpAccessor) &&
        services.Single(d => d.ServiceType == typeof(IClientIpAccessor)).Lifetime == ServiceLifetime.Scoped,
        "host supplies exactly one scoped client IP adapter in both composition orders");
      services.AddRazorPages();
      services.AddRazorComponents().AddInteractiveServerComponents();
      services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
      var timeouts = new List<TimeSpan>();
      services.Configure<HttpClientFactoryOptions>(nameof(IDirectoryApiClient), options =>
        options.HttpClientActions.Add(client => timeouts.Add(client.Timeout)));

      foreach (var type in new[] { typeof(IUnitOfWork), typeof(IGenericRepository<>), typeof(IBasicRepository<>),
        typeof(Core.Repositories.ISubdivisionRepository), typeof(IIpAddressRepository), typeof(IGenericRepository<Subdivision>), typeof(IGenericRepository<IpAddress>) })
        check(!services.Any(d => d.ServiceType == type), $"production DI has no legacy write registration {type.Name}");
      check(services.Count(d => d.ServiceType == typeof(IReadRepository<>)) == 1,
        "production DI has one open generic read repository registration after Scrutor");
      foreach (var (contract, implementation) in new[] {
        (typeof(IIpAccessService), typeof(IpAccessService)),
        (typeof(IPermissionService), typeof(PermissionService)),
        (typeof(IDataAccessScopeService), typeof(DataAccessScopeService)),
        (typeof(IIpManagementAccessService), typeof(IpManagementAccessService)),
        (typeof(IExportDeliveryService), typeof(ExportDeliveryService)) })
        check(services.Count(d => d.ServiceType == contract) == 1 &&
          services.Single(d => d.ServiceType == contract).ImplementationType == implementation &&
          services.Single(d => d.ServiceType == contract).Lifetime == ServiceLifetime.Scoped,
          $"production DI registers one scoped Application implementation for {contract.Name}");
      check(services.Count(d => d.ServiceType == typeof(IIpAccessReader)) == 1 &&
        services.Single(d => d.ServiceType == typeof(IIpAccessReader)).ImplementationType == typeof(IpAccessReader) &&
        services.Single(d => d.ServiceType == typeof(IIpAccessReader)).Lifetime == ServiceLifetime.Scoped,
        "production DI registers one scoped IP access reader without suffix scanning");
      check(services.Count(d => d.ServiceType == typeof(IPermissionReader)) == 1 &&
        services.Single(d => d.ServiceType == typeof(IPermissionReader)).ImplementationType == typeof(PermissionReader) &&
        services.Single(d => d.ServiceType == typeof(IPermissionReader)).Lifetime == ServiceLifetime.Scoped,
        "production DI registers one scoped permission reader without suffix scanning");
      check(services.Count(d => d.ServiceType == typeof(IDataAccessScopeReader)) == 1 &&
        services.Single(d => d.ServiceType == typeof(IDataAccessScopeReader)).ImplementationType == typeof(DataAccessScopeReader) &&
        services.Single(d => d.ServiceType == typeof(IDataAccessScopeReader)).Lifetime == ServiceLifetime.Scoped,
        "production DI registers one scoped subdivision access reader without suffix scanning");
      check(services.Count(d => d.ServiceType == typeof(RoleManager<ApplicationRole>)) == 1,
        "Identity supplies a single RoleManager registration");
      check(services.Count(d => d.ServiceType == typeof(IDirectoryApiClient)) == 1 &&
        services.Single(d => d.ServiceType == typeof(IDirectoryApiClient)).Lifetime == ServiceLifetime.Transient,
        "typed directory client is not overridden by scoped scanning");

      await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
      await using var first = provider.CreateAsyncScope();
      await using var second = provider.CreateAsyncScope();
      var a = first.ServiceProvider;
      var b = second.ServiceProvider;
      check(a.GetRequiredService<IIpManagementAccessService>() is IpManagementAccessService &&
        ReferenceEquals(a.GetRequiredService<IIpManagementAccessService>(), a.GetRequiredService<IIpManagementAccessService>()) &&
        !ReferenceEquals(a.GetRequiredService<IIpManagementAccessService>(), b.GetRequiredService<IIpManagementAccessService>()),
        "IP management decision resolves once per scope and is isolated between scopes");
      // Resolve every Guard service interface in the actual registration set, not a hand-copied list.
      foreach (var type in services.Select(d => d.ServiceType).Distinct().Where(t => t.IsInterface && !t.IsGenericTypeDefinition &&
        (t.Assembly == typeof(GuardServiceRegistration).Assembly || t.Assembly == typeof(ICurrentSession).Assembly ||
         t.Assembly == typeof(InfrastructureServiceRegistration).Assembly)))
        check(a.GetRequiredService(type) != null, $"production service graph resolves {type.Name}");
      check(a.GetRequiredService<IReadRepository<Personal>>() is ReadRepository<Personal> &&
        a.GetRequiredService<IReadRepository<ApplicationUser>>() is ReadRepository<ApplicationUser>, "closed read repositories resolve after legacy cleanup");
      check(a.GetRequiredService<UserManager<ApplicationUser>>() is PolicyUserManager &&
        a.GetRequiredService<SignInManager<ApplicationUser>>() is IpRestrictedSignInManager,
        "production Identity retains policy and IP-aware managers");
      check(a.GetRequiredService<ApplicationDbContext>().AuditWriter != null,
        "scoped Identity context uses mandatory audit persistence");
      var db = a.GetRequiredService<ApplicationDbContext>();
      check(ReferenceEquals(db, a.GetRequiredService<ApplicationDbContext>()) && !ReferenceEquals(db, b.GetRequiredService<ApplicationDbContext>()),
        "Identity context is shared within its scope and isolated between scopes");
      var store = a.GetRequiredService<IUserStore<ApplicationUser>>();
      check(ReferenceEquals(store.GetType().GetProperty("Context")!.GetValue(store), db),
        "Identity user store uses the scoped context rather than a factory context");
      var roleStore = a.GetRequiredService<IRoleStore<ApplicationRole>>();
      check(ReferenceEquals(roleStore.GetType().GetProperty("Context")!.GetValue(roleStore), db),
        "Identity role store shares the operation context with user store");
      check(db.GetService<IDbContextOptions>().Extensions.OfType<CoreOptionsExtension>().Single().Interceptors!
        .Contains(a.GetRequiredService<AuditSaveChangesInterceptor>()), "scoped Identity context retains audit interceptor");
      await using var independent = await provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();
      check(independent.AuditWriter != null, "factory context uses mandatory audit persistence");
      var factoryOptions = independent.GetService<IDbContextOptions>().Extensions.OfType<CoreOptionsExtension>().Single();
      check(!ReferenceEquals(independent, db) && !(factoryOptions.Interceptors?.Any() ?? false),
        "singleton factory creates an independent context without scoped audit interceptor");
      check(!factoryOptions.IsSensitiveDataLoggingEnabled && factoryOptions.DetailedErrorsEnabled == development,
        "factory diagnostics preserve environment detail without exposing SQL parameter values");
      var scopedOptions = db.GetService<IDbContextOptions>().Extensions.OfType<CoreOptionsExtension>().Single();
      check(!scopedOptions.IsSensitiveDataLoggingEnabled && scopedOptions.DetailedErrorsEnabled == development,
        "scoped diagnostics preserve environment detail without exposing Identity SQL parameter values");
      var calls = timeouts.Count;
      check(a.GetRequiredService<IDirectoryApiClient>() is DirectoryApiClient && b.GetRequiredService<IDirectoryApiClient>() is DirectoryApiClient &&
        timeouts.Count == calls + 2 && timeouts.All(t => t == TimeSpan.FromSeconds(60)),
        "resolved typed clients execute named HttpClient configuration with 60 second timeout");
      foreach (var (contract, implementation) in new[] {
          (typeof(ILogSearchService), typeof(LogService)), (typeof(IRoleSearchService), typeof(ApplicationRoleService)),
          (typeof(IClassifierManagementService), typeof(ClassifierService)),
          (typeof(IPersonalSearchService), typeof(PersonalService)), (typeof(IPersonalDetailsService), typeof(PersonalService)),
          (typeof(IPersonalEditorService), typeof(PersonalService)), (typeof(IPersonalStatusService), typeof(PersonalService)),
          (typeof(ISubdivisionManagementService), typeof(SubdivisionService)),
          (typeof(IIpAddressManagementService), typeof(IpAddressService)),
          (typeof(IUserAdministrationService), typeof(SecurityService)),
          (typeof(IUserListService), typeof(SecurityService)) }) {
        check(services.Count(d => d.ServiceType == contract) == 1 &&
          services.Single(d => d.ServiceType == contract).ImplementationType == implementation &&
          services.Single(d => d.ServiceType == contract).Lifetime == ServiceLifetime.Scoped,
            $"{contract.Name} has one scoped registration without legacy alias");
        check(a.GetRequiredService(contract).GetType() == implementation &&
          ReferenceEquals(a.GetRequiredService(contract), a.GetRequiredService(contract)) &&
          !ReferenceEquals(a.GetRequiredService(contract), b.GetRequiredService(contract)),
          $"{contract.Name} reuses its scope and stays isolated between scopes");
      }
      var identity = a.GetRequiredService<IOptions<IdentityOptions>>().Value;
      check(identity.Password.RequiredLength == 6 && identity.Password.RequiredUniqueChars == 1 &&
        !identity.Password.RequireDigit && !identity.Password.RequireLowercase && !identity.Password.RequireUppercase && !identity.Password.RequireNonAlphanumeric &&
        a.GetServices<IPasswordValidator<ApplicationUser>>().Any(p => p is PolicyPasswordValidator),
        "local Identity password defaults and policy validator survive extraction");
      check(provider.GetServices<IHostedService>().Count(s => s is AccountPolicyWorker or ApiSynchronizationWorker or ExportWorker) == 3,
        "background workers resolve without starting execution");
      check(a.GetRequiredService<IOptions<DirectoryApiOptions>>().Value.PageSize == 1234 &&
        a.GetRequiredService<IOptions<ExportOptions>>().Value.RowsPerPart == 200 &&
        a.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value.KnownProxies.Contains(System.Net.IPAddress.Parse("127.0.0.2")),
        "directory, export and trusted proxy configuration reach extracted registrations");
    }
  }
}
