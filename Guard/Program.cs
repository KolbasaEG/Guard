using Guard;
using Guard.Components.Account;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Interceptors;
using Guard.Core.Logging;
using Guard.Core.Repositories;
using Guard.Core.Services;
using Guard.Middlewares;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NpgsqlTypes;
using Radzen;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.PostgreSQL;
using Serilog.Sinks.Syslog;

var builder = WebApplication.CreateBuilder(args);
// =====================================================
// ПРОВЕРКА И СОЗДАНИЕ ФИЗИЧЕСКИХ БАЗ ДАННЫХ
// =====================================================
var logsConnectionString = builder.Configuration.GetConnectionString("LogsConnection")
    ?? throw new InvalidOperationException("Строка подключения 'LogsConnection' не найдена.");

var defaultConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Строка подключения 'DefaultConnection' не найдена.");
// Строка подключения для будущей третьей БД:
// var thirdConnectionString = builder.Configuration.GetConnectionString("ThirdConnection");

// Создаем БД логов и таблицу 'logs'
DatabaseInitializer.EnsureLogsDatabaseExists(logsConnectionString);

// Создаем физические базы данных для остальных контекстов
DatabaseInitializer.EnsureDatabaseExists(defaultConnectionString);
// if (!string.IsNullOrEmpty(thirdConnectionString)) DatabaseInitializer.EnsureDatabaseExists(thirdConnectionString);

// =====================================================
// === SERILOG: ОСНОВНОЙ ЛОГГЕР
// =====================================================
IDictionary<string, ColumnWriterBase> columnWriters = new Dictionary<string, ColumnWriterBase>
{
  { "message", new RenderedMessageColumnWriter() },
  { "message_template", new MessageTemplateColumnWriter() },
  { "level", new LevelColumnWriter(true, NpgsqlDbType.Varchar) },
  { "layer", new SinglePropertyColumnWriter("Layer", PropertyWriteMethod.Raw, NpgsqlDbType.Varchar) },
  { "timestamp", new TimestampColumnWriter() },
  { "exception", new ExceptionColumnWriter() },
  { "properties", new LogEventSerializedColumnWriter() },
  { "user_id", new SinglePropertyColumnWriter("UserId", PropertyWriteMethod.Raw, NpgsqlDbType.Varchar) },
  { "ip_address", new SinglePropertyColumnWriter("ClientIp", PropertyWriteMethod.Raw, NpgsqlDbType.Varchar) }
};

// 1. Включаем SelfLog (запись ошибок сети/БД в локальный файл, если PostgreSQL упадет)
Serilog.Debugging.SelfLog.Enable(msg =>
{
  Console.Error.WriteLine($"[Serilog Error] {msg}");
  Directory.CreateDirectory("logs");
  File.AppendAllText("logs/serilog-internal-errors.log", $"{DateTime.UtcNow:o} {msg}{Environment.NewLine}");
});

// 2. Загружаем сохраненный уровень логирования
var savedState = LogLevelPersistenceService.LoadState();

var levelSwitch = new LoggingLevelSwitch(savedState.MinimumLevel);
builder.Services.AddSingleton(levelSwitch);

// 3. Создаем и регистрируем динамический менеджер логгера
var loggerManager = new DynamicLoggerManager(logsConnectionString, levelSwitch);
builder.Services.AddSingleton(loggerManager);

loggerManager.ApplyConfiguration(maxSessions: savedState.MaxSessions, newLevel: savedState.MinimumLevel);

builder.Host.UseSerilog();

// =====================================================
// === DEDICATED AUDIT LOGGER (Syslog / CEF)
// =====================================================
var auditTcpConfig = new SyslogTcpConfig
{
  Host = builder.Configuration.GetValue<string>("Audit:Syslog:Host") ?? "localhost",
  Port = builder.Configuration.GetValue<int>("Audit:Syslog:Port", 6514),
  UseTls = true,
  Framer = new MessageFramer(FramingType.OCTET_COUNTING),
  Formatter = new Rfc5424Formatter(),

  CertValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
  {
    if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
      return true;

    Log.Warning("RuSIEM TLS validation warning: {Errors}", sslPolicyErrors);
    return true;
  }
};

var auditLogger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.TcpSyslog(auditTcpConfig)
    .CreateLogger();

builder.Services.AddSingleton<IAuditService>(_ => new AuditService(auditLogger));

// === Регистрация Интерцептора как Scoped (Исправлено!) ===
builder.Services.AddScoped<AuditSaveChangesInterceptor>();

// =====================================================
// === DATABASE (DbContextFactory + Scoped DbContext)
// =====================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 1. Фабрика контекстов для фоновых/параллельных задач
builder.Services.AddDbContextFactory<ApplicationDbContext>((sp, options) =>
{
  var env = sp.GetRequiredService<IWebHostEnvironment>();
  options.UseNpgsql(connectionString);
  options.EnableDetailedErrors(builder.Environment.IsDevelopment());
  options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
  // Включаем подробные данные ТОЛЬКО при локальной разработке
  if (env.IsDevelopment())
  {
    options.EnableSensitiveDataLogging();
    options.EnableDetailedErrors();
  }
});

// 2. Scoped DbContext для UnitOfWork (подключаем интерцептор аудита)
builder.Services.AddScoped<ApplicationDbContext>(sp =>
{
  var env = sp.GetRequiredService<IWebHostEnvironment>();
  var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

  optionsBuilder.UseNpgsql(connectionString);
  // Подключаем интерцептор из текущего Scoped-контекста
  optionsBuilder.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());

  optionsBuilder.EnableDetailedErrors(builder.Environment.IsDevelopment());
  optionsBuilder.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
  // Включаем подробные данные ТОЛЬКО при локальной разработке
  if (env.IsDevelopment())
  {
    optionsBuilder.EnableSensitiveDataLogging();
    optionsBuilder.EnableDetailedErrors();
  }
  return new ApplicationDbContext(optionsBuilder.Options);
});

// 3. Фабрика для работы с изолированной БД логов
builder.Services.AddDbContextFactory<LogsDbContext>(options =>
{
  options.UseNpgsql(builder.Configuration.GetConnectionString("LogsConnection"));
});

// =====================================================
// === IDENTITY & AUTH
// =====================================================
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<RoleManager<ApplicationRole>>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider<ApplicationUser>>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddHttpContextAccessor();

// =====================================================
// === REPOSITORIES & SERVICES (Scrutor)
// =====================================================
builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped(typeof(IReadRepository<>), typeof(ReadRepository<>));
builder.Services.AddScoped(typeof(IBasicRepository<>), typeof(BasicRepository<>));

builder.Services.Scan(scan => scan
    .FromAssemblies(typeof(Program).Assembly)
    .AddClasses(classes => classes.Where(type =>
        (type.Name.EndsWith("Repository") || type.Name.EndsWith("Service")) &&
        !type.Name.Contains("GenericRepository") &&
        !type.Name.Contains("BasicRepository") &&
        !type.Name.Contains("UnitOfWork") &&
        !type.Name.Contains("ScopeService") &&
        !type.Name.Contains("CurrentUserService") &&
        !type.Name.Contains("AuditService") &&
        !type.Name.Contains("LogService")))
    .AsImplementedInterfaces()
    .WithScopedLifetime());

// =====================================================
// === RADZEN & BLAZOR COMPONENTS
// =====================================================
builder.Services.AddSignalR();
builder.Services.AddRazorPages();

// AddRadzenComponents уже включает DialogService, NotificationService, TooltipService, ContextMenuService
builder.Services.AddRadzenComponents();

builder.Services.AddRadzenCookieThemeService(options =>
{
  options.Name = "ApplicationTheme";
  options.Duration = TimeSpan.FromDays(365);
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

// =====================================================
// === MIDDLEWARE PIPELINE
// =====================================================
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseUserContextLogging();
app.UseAntiforgery();
app.MapRazorPages();
app.MapRazorComponents<Guard.Components.App>()
    .AddInteractiveServerRenderMode();
app.MapAdditionalIdentityEndpoints();

// === SEEDING ===
if (app.Environment.IsDevelopment())
{
  using var scope = app.Services.CreateScope();
  var services = scope.ServiceProvider;

  var context = services.GetRequiredService<ApplicationDbContext>();
  var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
  var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

  await DatabaseSeeder.SeedAsync(context, userManager, roleManager);
}

app.Run();