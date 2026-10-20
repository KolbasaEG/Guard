using Guard.Core.Contexts;
using Microsoft.EntityFrameworkCore;
using Guard.Core.Services.DTOs;
using Guard.Core.Identity;

namespace Guard.Core.Services;

public class LogService : ILogSearchService
{
  private readonly IDbContextFactory<LogsDbContext> _logsFactory;
  private readonly IPermissionService _permissions;

  public LogService(
      IDbContextFactory<LogsDbContext> logsFactory,
      IPermissionService permissions)
  {
    _logsFactory = logsFactory;
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
      .Select(e => new LogEntryDto(e.Timestamp, e.Level, e.Layer, e.Message, e.Exception, e.UserId, e.ClientIp, e.Id, e.Properties, e.MessageTemplate)).ToListAsync(ct);
    await SearchAccess.RecheckAsync(_permissions, Permissions.Logs.Read, actor, ct);
    return new LogSearchResult(items, count);
  }
}
