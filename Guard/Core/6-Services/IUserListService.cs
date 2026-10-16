using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

public interface IUserListService
{
  Task<UserSearchResult> SearchUsersAsync(UserSearchRequest request, CancellationToken ct = default);
  Task SetUserLockoutAsync(string userId, bool lockout, CancellationToken ct = default);
}
