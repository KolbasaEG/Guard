using Guard.Core.Entities;

namespace Guard.Core.Services;

public interface ILogService : ILogSearchService
{
  Task<TResult> GetLogsAsync<TResult>(
      Func<IQueryable<LogEntry>, Task<TResult>> query,
      CancellationToken ct = default);
}
