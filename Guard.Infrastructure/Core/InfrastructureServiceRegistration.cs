using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Interceptors;
using Guard.Core.Logging;
using Guard.Core.Repositories;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Guard.Infrastructure;

// Registration only: no database initialization, migrations, seeding or host startup.
public static class InfrastructureServiceRegistration
{
  public static IServiceCollection AddGuardInfrastructure(this IServiceCollection services, IConfiguration configuration, bool development)
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
    });

    // 2. Scoped DbContext для Identity stores в HTTP / отдельном scope операции.
    services.AddScoped<ApplicationDbContext>(sp =>
    {
      var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

      optionsBuilder.UseNpgsql(connectionString);
      // Подключаем интерцептор из текущего Scoped-контекста
      optionsBuilder.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());

      optionsBuilder.EnableDetailedErrors(development);
      return new ApplicationDbContext(optionsBuilder.Options, sp.GetRequiredService<AuditPersistence>());
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

    // =====================================================
    // === REPOSITORIES & SERVICES (Scrutor)
    // =====================================================
    services.AddScoped<ILogSearchService, LogService>();
    services.AddSingleton<InternalLogWriter>();
    services.AddSingleton<AuditHealth>();
    services.AddSingleton(sp => new AuditJournal(configuration.GetConnectionString("DefaultConnection")
      ?? throw new InvalidOperationException("Не настроена основная БД аудита."),
      sp.GetRequiredService<InternalLogWriter>(), sp.GetRequiredService<AuditHealth>()));
    services.AddSingleton<IOperationAvailability>(sp => sp.GetRequiredService<AuditJournal>());
    services.AddSingleton<AuditPersistence>();
    services.AddOptions<AuditSyslogOptions>().Bind(configuration.GetSection("Audit:Syslog"))
      .Validate(o => !string.IsNullOrWhiteSpace(o.Host) && o.Port is >= 1 and <= 65535, "Некорректный адрес SIEM.");
    services.AddSingleton<IAuditTransport, SyslogAuditTransport>();
    services.AddSingleton<AuditDeliveryProcessor>();
    services.AddSingleton<LogRetention>();
    services.AddScoped<IUnitOfWorkFactory, UnitOfWorkFactory>();
    services.AddHttpContextAccessor();
    services.AddSingleton<IAccessChangeNotifier, AccessChangeNotifier>();
    services.AddScoped<ICurrentUserDisplayService, CurrentUserService>();
    services.AddScoped(typeof(IReadRepository<>), typeof(ReadRepository<>));
    services.AddScoped<IIpAccessReader, IpAccessReader>();
    services.AddScoped<IPermissionReader, PermissionReader>();
    services.AddScoped<IDataAccessScopeReader, DataAccessScopeReader>();

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
        .FromAssemblies(typeof(InfrastructureServiceRegistration).Assembly)
        .AddClasses(classes => classes.Where(type =>
            type.Name.EndsWith("Service") &&
            type != typeof(CurrentUserService) &&
            type != typeof(AuditService) &&
            type != typeof(LogService) &&
            type != typeof(DirectoryApiClient)))
        .AsImplementedInterfaces()
        .WithScopedLifetime());
    return services;
  }
}
