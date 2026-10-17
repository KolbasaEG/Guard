using Guard.Components.Account;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Interceptors;
using Guard.Core.Repositories;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Guard;

// Registration only: no database initialization, migrations, seeding or host startup.
public static class GuardServiceRegistration
{
  public static IServiceCollection AddGuardServices(this IServiceCollection services, IConfiguration configuration, bool development)
  {
    // Аудит scoped-контекста Identity; фабричные записи задают автора отдельно.
    services.AddScoped<AuditSaveChangesInterceptor>();

    // =====================================================
    // === DATABASE (DbContextFactory + Scoped DbContext)
    // =====================================================
    var connectionString = configuration.GetConnectionString("DefaultConnection");

    // 1. Фабрика контекстов для фоновых/параллельных задач
    services.AddDbContextFactory<ApplicationDbContext>(options =>
    {
      options.UseNpgsql(connectionString);
      options.EnableDetailedErrors(development);
      options.EnableSensitiveDataLogging(development);
    });

    // 2. Scoped DbContext для Identity stores в HTTP / отдельном scope операции.
    services.AddScoped<ApplicationDbContext>(sp =>
    {
      var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

      optionsBuilder.UseNpgsql(connectionString);
      // Подключаем интерцептор из текущего Scoped-контекста
      optionsBuilder.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());

      optionsBuilder.EnableDetailedErrors(development);
      optionsBuilder.EnableSensitiveDataLogging(development);
      return new ApplicationDbContext(optionsBuilder.Options);
    });

    // 3. Фабрика для работы с изолированной БД логов
    services.AddDbContextFactory<LogsDbContext>(options =>
    {
      options.UseNpgsql(configuration.GetConnectionString("LogsConnection"));
    });

    // =====================================================
    // === IDENTITY & AUTH
    // =====================================================
    services.AddIdentity<ApplicationUser, ApplicationRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders()
        .AddUserManager<PolicyUserManager>()
        .AddSignInManager<IpRestrictedSignInManager>();

    services.AddScoped<IPasswordValidator<ApplicationUser>, PolicyPasswordValidator>();
    services.Configure<IdentityOptions>(options => {
      options.Password.RequiredLength = 6;
      options.Password.RequiredUniqueChars = 1;
      options.Password.RequireDigit = false;
      options.Password.RequireLowercase = false;
      options.Password.RequireUppercase = false;
      options.Password.RequireNonAlphanumeric = false;
    });
    services.AddHostedService<AccountPolicyWorker>();

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
    services.AddHttpContextAccessor();
    services.AddScoped<IDataAccessScopeService, DataAccessScopeService>();
    services.AddSingleton<IAccessChangeNotifier, AccessChangeNotifier>();
    services.AddScoped<AccessRefreshCoordinator>();
    services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, AccessCircuitHandler>();
    services.AddAuthorization(options =>
    {
      foreach (var permission in PermissionCatalog.All)
        options.AddPolicy(permission.Code, policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission.Code)));
    });
    services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionAuthorizationHandler>();

    // =====================================================
    // === REPOSITORIES & SERVICES (Scrutor)
    // =====================================================
    services.AddScoped<ILogService, LogService>();
    services.AddScoped<ILogSearchService>(services => services.GetRequiredService<ILogService>());
    services.AddScoped<IUnitOfWorkFactory, UnitOfWorkFactory>();
    services.AddScoped<ICurrentSession, CurrentSessionAdapter>();
    services.AddScoped<ICurrentUserDisplayService, CurrentUserService>();
    services.AddScoped(typeof(IReadRepository<>), typeof(ReadRepository<>));
    services.AddScoped<IIpAccessReader, IpAccessReader>();

    services.Configure<DirectoryApiOptions>(configuration.GetSection("DirectoryApi"));
    var dataProtection = services.AddDataProtection();
    var keyDirectory = configuration["DataProtection:KeyDirectory"];
    if (!string.IsNullOrWhiteSpace(keyDirectory)) dataProtection.PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(keyDirectory)));
    var applicationName = configuration["DataProtection:ApplicationName"];
    if (!string.IsNullOrWhiteSpace(applicationName)) dataProtection.SetApplicationName(applicationName);
    var keyCertificate = configuration["DataProtection:CertificateThumbprint"];
    if (!string.IsNullOrWhiteSpace(keyCertificate)) dataProtection.ProtectKeysWithCertificate(keyCertificate);
    services.AddSingleton<ApiCredentialProtector>();
    services.AddHttpClient<IDirectoryApiClient, DirectoryApiClient>(client => client.Timeout = TimeSpan.FromSeconds(60));
    services.AddScoped<ApiSynchronizationRunner>();
    services.AddHostedService<ApiSynchronizationWorker>();
    services.AddOptions<ExportOptions>().Bind(configuration.GetSection("Exports"))
      .Validate(o => o.RowsPerPart is >= 100 and <= 1000000 && o.MaxQueuedPerUser is >= 1 and <= 20 && o.TimeoutMinutes is >= 1 and <= 1440 && o.MinimumFreeBytes >= 0 && !string.IsNullOrWhiteSpace(o.Directory), "Некорректные настройки экспорта.")
      .ValidateOnStart();
    services.AddSingleton<ExportStorage>();
    services.AddScoped<ExportAccess>();
    services.AddScoped<ExportSnapshot>();
    services.AddScoped<ExportDocumentWriter>();
    services.AddScoped<ExportProcessor>();
    services.AddHostedService<ExportWorker>();

    services.Scan(scan => scan
        .FromAssemblies(typeof(GuardServiceRegistration).Assembly)
        .AddClasses(classes => classes.Where(type =>
            type.Name.EndsWith("Service") &&
            type != typeof(DataAccessScopeService) &&
            type != typeof(CurrentUserService) &&
            type != typeof(AuditService) &&
            type != typeof(LogService) &&
            type != typeof(DirectoryApiClient)))
        .AsImplementedInterfaces()
        .WithScopedLifetime());
    return services;
  }
}
