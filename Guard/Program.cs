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
using Radzen;
using Serilog;
using Serilog.Core;
using System.Threading.RateLimiting;

if (await Guard.Web.Maintenance.IpCatalogMaintenanceCommand.TryRunAsync(args)) return;

var diagnostics = new InternalLogWriter();
Serilog.Debugging.SelfLog.Enable(diagnostics.Write);
using var bootstrap = new LoggerConfiguration().MinimumLevel.Information()
  .WriteTo.Console()
  .WriteTo.File(new Serilog.Formatting.Json.JsonFormatter(), "logs/startup-.json", rollingInterval: RollingInterval.Day,
    fileSizeLimitBytes: 10 * 1024 * 1024, rollOnFileSizeLimit: true, retainedFileCountLimit: null,
    retainedFileTimeLimit: null).CreateLogger();
Log.Logger = bootstrap;
DynamicLoggerManager? loggerManager = null;
Logger? auditLogger = null;
try {
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
  try { DatabaseInitializer.EnsureLogsDatabaseExists(logsConnectionString); }
  catch (Exception error) {
    bootstrap.Warning(error, "БД технических логов недоступна. Локальная запись продолжает работать");
  }

  // Создаем физические базы данных для остальных контекстов
  try { DatabaseInitializer.EnsureDatabaseExists(defaultConnectionString); }
  catch (Exception error) when (error is System.Data.Common.DbException or TimeoutException) {
    bootstrap.Error(error, "Основная БД недоступна. Рабочие операции запрещены; доступна публичная страница отказа");
  }
  // if (!string.IsNullOrEmpty(thirdConnectionString)) DatabaseInitializer.EnsureDatabaseExists(thirdConnectionString);

  // =====================================================
  // === SERILOG: ОСНОВНОЙ ЛОГГЕР
  // =====================================================
  // 2. Загружаем сохраненный уровень логирования
  var savedState = LogLevelPersistenceService.LoadState();

  var levelSwitch = new LoggingLevelSwitch(savedState.MinimumLevel);
  builder.Services.AddSingleton(levelSwitch);

  // 3. Создаем и регистрируем динамический менеджер логгера
  loggerManager = new DynamicLoggerManager(logsConnectionString, levelSwitch);
  builder.Services.AddSingleton(loggerManager);

  loggerManager.ApplyConfiguration(maxSessions: savedState.MaxSessions, newLevel: savedState.MinimumLevel);

  Log.Logger = loggerManager.Logger;
  builder.Host.UseSerilog();

  // =====================================================
  // === DEDICATED AUDIT LOGGER (Syslog / CEF)
  // =====================================================
  auditLogger = new LoggerConfiguration()
      .MinimumLevel.Information()
      .Enrich.FromLogContext()
      .WriteTo.Console()
      .WriteTo.File(new Serilog.Formatting.Json.JsonFormatter(), "logs/audit-.json", rollingInterval: RollingInterval.Day,
        fileSizeLimitBytes: 10 * 1024 * 1024, rollOnFileSizeLimit: true, retainedFileCountLimit: null, retainedFileTimeLimit: null)
      .CreateLogger();

  builder.Services.AddSingleton<IAuditService>(sp => new AuditService(auditLogger, sp.GetRequiredService<AuditJournal>()));
  builder.Services.AddHostedService<AuditWorker>();

  builder.Services.AddGuardServices(builder.Configuration, builder.Environment.IsDevelopment());
  builder.Services.AddRateLimiter(options => {
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (rejected, ct) => {
      var http = rejected.HttpContext;
      try {
        await http.RequestServices.GetRequiredService<IAuditService>().RecordAsync(
          AuditEventData.Create(Guard.Core.Enums.AuditEventType.LoginFailed, null, ClientIpAddress.Get(http)) with {
            Actor = null, ActorKind = "anonymous", Outcome = "denied", ReasonCode = "RateLimited",
            CorrelationId = http.TraceIdentifier, ObjectType = "Account"
          }, ct);
      }
      catch (Exception error) {
        diagnostics.Write($"AuditRateLimitedLoginFailed ErrorType={error.GetType().Name}");
      }
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
      HttpMethods.IsPost(context.Request.Method) && string.Equals(context.Request.Path.Value?.TrimEnd('/'), "/Account/Login", StringComparison.OrdinalIgnoreCase)
        ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
          new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
        : RateLimitPartition.GetNoLimiter("other"));
  });

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


  // =====================================================
  // === MIDDLEWARE PIPELINE
  // =====================================================
  app.UseForwardedHeaders();
  app.UseStaticFiles();
  app.UseRouting();
  // Скомпилированные публичные ресурсы, включая версионированный загрузчик Blazor.
  app.MapStaticAssets().ShortCircuit();

  app.UseAuthentication();
  app.UseRateLimiter();
  app.UseUserContextLogging();
  app.UseSerilogRequestLogging();
  app.UseMiddleware<AuditAvailabilityMiddleware>();
  app.UseAuthorization();
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

    try { await DatabaseSeeder.SeedAsync(context, userManager, roleManager); }
    catch (Exception error) when (error is System.Data.Common.DbException or TimeoutException) {
      bootstrap.Error(error, "Подготовка Development-БД недоступна. Для восстановления требуется обслуживание сервера");
    }
  }

  app.Run();
}
catch (Exception error) {
  bootstrap.Fatal(error, "Приложение Guard завершилось с ошибкой");
  throw;
}
finally {
  try { auditLogger?.Dispose(); }
  finally {
    try { loggerManager?.Dispose(); }
    finally { Log.CloseAndFlush(); Serilog.Debugging.SelfLog.Disable(); }
  }
}
