using Guard.Core.Identity;
using Guard.Core.Logging;
using Serilog.Events;

namespace Guard.Core.Services;

public sealed class LogConfigurationService(IPermissionService permissions, DynamicLoggerManager manager,
    ILogger<LogConfigurationService> logger) : ILogConfigurationService
{
  public async Task SaveAsync(int maxSessions, LogEventLevel level, CancellationToken ct = default)
  {
    await permissions.RequireAsync(Permissions.Logs.Configure, ct);
    var actor = await permissions.GetCurrentAsync(ct);
    ct.ThrowIfCancellationRequested();
    manager.ApplyConfiguration(maxSessions, level, () => LogLevelPersistenceService.SaveState(new() { MaxSessions = maxSessions, MinimumLevel = level }));
    logger.LogWarning("Настройки логирования изменены пользователем {UserId}: {Level}, {MaxSessions}", actor.UserId, level, maxSessions);
  }
}
