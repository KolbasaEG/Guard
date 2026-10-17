using Guard.Web.Exports;
using Guard;
using Guard.Components.Account;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Logging;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Guard.Middlewares;
using Guard.Core.Identity;
using Microsoft.AspNetCore.Identity;
using NpgsqlTypes;
using Radzen;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.PostgreSQL;
using Serilog.Sinks.Syslog;

if (await Guard.Web.Maintenance.IpCatalogMaintenanceCommand.TryRunAsync(args)) return;

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
  { "timestamp", new UtcTimestampColumnWriter() },
  { "exception", new ExceptionColumnWriter() },
  { "properties", new LogEventSerializedColumnWriter() },
  { "user_id", new SinglePropertyColumnWriter("UserId", PropertyWriteMethod.Raw, NpgsqlDbType.Varchar) },
  { "ip_address", new SinglePropertyColumnWriter("ClientIp", PropertyWriteMethod.Raw, NpgsqlDbType.Varchar) }
};

// 1. Включаем SelfLog (запись ошибок сети/БД в локальный файл, если PostgreSQL упадет)
var selfLogGate = new object();
Serilog.Debugging.SelfLog.Enable(msg =>
{
  lock (selfLogGate) {
  Console.Error.WriteLine($"[Serilog Error] {msg}");
  Directory.CreateDirectory("logs");
  File.AppendAllText("logs/serilog-internal-errors.log", $"{DateTime.UtcNow:o} {msg}{Environment.NewLine}");
  }
});

// 2. Загружаем сохраненный уровень логирования
var savedState = LogLevelPersistenceService.LoadState();

var levelSwitch = new LoggingLevelSwitch(savedState.MinimumLevel);
builder.Services.AddSingleton(levelSwitch);

// 3. Создаем и регистрируем динамический менеджер логгера
var loggerManager = new DynamicLoggerManager(logsConnectionString, levelSwitch);
Log.Logger = loggerManager.Logger;
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

builder.Services.AddGuardServices(builder.Configuration, builder.Environment.IsDevelopment());

// =====================================================
// === RADZEN & BLAZOR COMPONENTS
// =====================================================
builder.Services.AddSignalR();
builder.Services.AddRazorPages();

// AddRadzenComponents уже включает DialogService, NotificationService, TooltipService, ContextMenuService
builder.Services.AddRadzenComponents();
builder.Services.AddScoped<Guard.Components.Library.BrowserTimeService>();

builder.Services.AddRadzenCookieThemeService(options =>
{
  options.Name = "ApplicationTheme";
  options.Duration = TimeSpan.FromDays(365);
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();
app.Lifetime.ApplicationStopped.Register(loggerManager.Dispose);

// =====================================================
// === MIDDLEWARE PIPELINE
// =====================================================
app.UseForwardedHeaders();
app.UseStaticFiles();
app.UseRouting();
// Скомпилированные публичные ресурсы, включая версионированный загрузчик Blazor.
app.MapStaticAssets().ShortCircuit();

app.UseAuthentication();
app.UseAuthorization();
app.UseUserContextLogging();
app.UseMiddleware<IpCheckMiddleware>();
app.UseMiddleware<AccountPolicyMiddleware>();
app.UseAntiforgery();
app.MapRazorPages();
app.MapRazorComponents<Guard.Components.App>()
    .AddInteractiveServerRenderMode();
app.MapAdditionalIdentityEndpoints();
app.MapExportEndpoints();

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
