using Guard.Core.Contexts;
using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Guard.Core.Services.DTOs;
using Guard.Core.Identity;

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

  public async Task<LogSearchResult> SearchAsync(LogSearchRequest request, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    var actor = await SearchAccess.RequireAsync(_permissions, Permissions.Logs.Read, ct);
    if (request.Skip < 0 || request.Take is < 1 or > 100) throw new ArgumentException("Недопустимые параметры страницы.");
    LogSearchQuery.ValidateFilter(request.Filter);
    await using var context = await _logsFactory.CreateDbContextAsync(ct);
    var query = LogSearchQuery.Filter(context.Logs.AsNoTracking(), request.Filter);
    var count = await query.CountAsync(ct);
    var items = await LogSearchQuery.Sort(query, request.OrderBy).Skip(request.Skip).Take(request.Take)
      .Select(e => new LogEntryDto(e.Timestamp, e.Level, e.Layer, e.Message, e.Exception, e.UserId, e.ClientIp)).ToListAsync(ct);
    await SearchAccess.RecheckAsync(_permissions, Permissions.Logs.Read, actor, ct);
    return new LogSearchResult(items, count);
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
