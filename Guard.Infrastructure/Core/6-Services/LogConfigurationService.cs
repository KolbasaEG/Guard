using Guard.Core.Identity;
using Guard.Core.Enums;
using Guard.Core.Logging;
using Guard.Core.Services.DTOs;
using Serilog.Events;

namespace Guard.Core.Services;

public sealed class LogConfigurationService(IPermissionService permissions, DynamicLoggerManager manager,
    ILogger<LogConfigurationService> logger, IAuditService audit, ITechnicalLogService? technical = null) : ILogConfigurationService
{
  private static readonly SemaphoreSlim configurationGate = new(1, 1);
  public async Task<LogConfigurationDto> GetAsync(CancellationToken ct = default)
  {
    await permissions.RequireAsync(Permissions.Logs.Read, ct);
    ct.ThrowIfCancellationRequested();
    var state = manager.GetConfiguration();
    if (technical != null) return new(state.MaxSessions, (await technical.GetAsync(ct)).Settings.NormalLevel);
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
    if (technical != null) {
      if (settings.MaxSessions != manager.GetConfiguration().MaxSessions)
        throw new ArgumentException("Параметр сессий больше не используется. Измените объём буфера в настройках технического логирования.");
      var current = await technical.GetAsync(ct);
      var saved = await technical.SaveAsync(current.Version, current.Settings with { NormalLevel = settings.MinimumLevel }, ct);
      return new(settings.MaxSessions, saved.Settings.NormalLevel);
    }
    await configurationGate.WaitAsync(ct);
    try {
      var previous = manager.GetConfiguration();
      var intent = AuditEventData.Create(AuditEventType.LoggingConfigurationChangeRequested, actor.UserId,
        details: $"MinimumLevel={previous.MinimumLevel}->{level}; MaxSessions={previous.MaxSessions}->{settings.MaxSessions}")
        with { Outcome = "started", ObjectType = "LoggingConfiguration", ObjectId = "1" };
      // Файл и PostgreSQL не имеют общей транзакции: сначала долговечное намерение, затем результат.
      await audit.RecordAsync(intent, ct);
      try {
        manager.ApplyConfiguration(settings.MaxSessions, level, () => LogLevelPersistenceService.SaveState(new() { MaxSessions = settings.MaxSessions, MinimumLevel = level }));
      }
      catch (Exception error) {
        await audit.RecordAsync(intent with { EventId = Guid.NewGuid(), EventType = AuditEventType.LoggingConfigurationChanged,
          Outcome = "failure", ReasonCode = "ConfigurationApplyFailed" }, ct);
        logger.LogError(error, "Не удалось изменить настройки логирования");
        throw;
      }
      try {
        await audit.RecordAsync(intent with { EventId = Guid.NewGuid(), EventType = AuditEventType.LoggingConfigurationChanged,
          Outcome = "success", ReasonCode = "ConfigurationApplied" }, ct);
      }
      catch (Exception error) {
        logger.LogError(error, "Настройки логирования применены, но результат не подтверждён аудитом");
        throw new OperationCommitUnknownException(intent.CorrelationId, error);
      }
    }
    finally { configurationGate.Release(); }
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
