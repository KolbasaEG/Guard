using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

public interface ICurrentUserDisplayService
{
  Task<CurrentUserDisplayDto?> GetDisplayAsync(CancellationToken ct = default);
}
