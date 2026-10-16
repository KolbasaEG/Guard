using Guard.Core.Identity;
using Guard.Core.Logging;
using Guard.Core.Services.DTOs;
using Serilog.Events;

namespace Guard.Core.Services;

public sealed class LogConfigurationService(IPermissionService permissions, DynamicLoggerManager manager,
    ILogger<LogConfigurationService> logger) : ILogConfigurationService
{
  public async Task<LogConfigurationDto> GetAsync(CancellationToken ct = default)
  {
    await permissions.RequireAsync(Permissions.Logs.Read, ct);
    ct.ThrowIfCancellationRequested();
    var state = manager.GetConfiguration();
    return new(state.MaxSessions, FromSerilog(state.MinimumLevel));
  }

  public async Task<LogConfigurationDto> SaveAsync(LogConfigurationDto settings, CancellationToken ct = default)
  {
    await permissions.RequireAsync(Permissions.Logs.Configure, ct);
    var actor = await permissions.GetCurrentAsync(ct);
    if (!actor.Has(Permissions.Logs.Configure)) throw new UnauthorizedAccessException();
    ArgumentNullException.ThrowIfNull(settings);
    if (settings.MaxSessions is < 1 or > 10000)
      throw new ArgumentException("Укажите число сессий от 1 до 10000.");
    var level = ToSerilog(settings.MinimumLevel);
    ct.ThrowIfCancellationRequested();
    manager.ApplyConfiguration(settings.MaxSessions, level, () => LogLevelPersistenceService.SaveState(new() { MaxSessions = settings.MaxSessions, MinimumLevel = level }));
    logger.LogWarning("Настройки логирования изменены пользователем {UserId}: {Level}, {MaxSessions}", actor.UserId, level, settings.MaxSessions);
    return settings;
  }

  private static LogEventLevel ToSerilog(ApplicationLogLevel level) => level switch {
    ApplicationLogLevel.Verbose => LogEventLevel.Verbose,
    ApplicationLogLevel.Debug => LogEventLevel.Debug,
    ApplicationLogLevel.Information => LogEventLevel.Information,
    ApplicationLogLevel.Warning => LogEventLevel.Warning,
    ApplicationLogLevel.Error => LogEventLevel.Error,
    ApplicationLogLevel.Fatal => LogEventLevel.Fatal,
    _ => throw new ArgumentException("Укажите корректный уровень логирования.")
  };

  private static ApplicationLogLevel FromSerilog(LogEventLevel level) => level switch {
    LogEventLevel.Verbose => ApplicationLogLevel.Verbose,
    LogEventLevel.Debug => ApplicationLogLevel.Debug,
    LogEventLevel.Information => ApplicationLogLevel.Information,
    LogEventLevel.Warning => ApplicationLogLevel.Warning,
    LogEventLevel.Error => ApplicationLogLevel.Error,
    LogEventLevel.Fatal => ApplicationLogLevel.Fatal,
    _ => throw new ArgumentException("Укажите корректный уровень логирования.")
  };
}
