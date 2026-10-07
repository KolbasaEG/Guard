using Guard.Core.Contexts;
using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Guard.Core.Services;

public class LogService : ILogService
{
  private readonly IDbContextFactory<LogsDbContext> _logsFactory;
  private readonly ILogger<LogService> _logger;
  private readonly IPermissionService _permissions;

  public LogService(
      IDbContextFactory<LogsDbContext> logsFactory,
      ILogger<LogService> logger, IPermissionService permissions)
  {
    _logsFactory = logsFactory;
    _logger = logger;
    _permissions = permissions;
  }

  public async Task<TResult> GetLogsAsync<TResult>(
      Func<IQueryable<LogEntry>, Task<TResult>> query,
      CancellationToken ct = default)
  {
    await _permissions.RequireAsync(Guard.Core.Identity.Permissions.Logs.Read, ct);
    try
    {
      _logger.LogDebug("Выполнение запроса чтения из базы данных логов");

      await using var context = await _logsFactory.CreateDbContextAsync(ct);
      return await query(context.Logs.AsNoTracking());
    }
    catch (OperationCanceledException)
    {
      _logger.LogInformation("Запрос чтения системных логов был отменен");
      throw;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Произошла ошибка при выполнении выборки системных логов");
      throw;
    }
  }
}
