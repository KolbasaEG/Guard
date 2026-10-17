using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

public interface IRoleSearchService
{
  Task<RoleSearchResult> SearchAsync(RoleSearchRequest request, CancellationToken ct = default);
}

public interface ILogSearchService
{
  Task<LogSearchResult> SearchAsync(LogSearchRequest request, CancellationToken ct = default);
}
