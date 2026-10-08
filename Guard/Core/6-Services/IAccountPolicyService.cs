using Guard.Core.Entities;

namespace Guard.Core.Services;

public record AccountState(bool Blocked, bool PasswordExpired);

public interface IAccountPolicyService
{
  Task<AccountPolicy> GetAsync(CancellationToken ct = default);
  Task SaveAsync(AccountPolicy policy, CancellationToken ct = default);
  Task<AccountState> CheckAsync(string userId, CancellationToken ct = default);
  Task RecordActivityAsync(string userId, CancellationToken ct = default);
  Task SweepAsync(CancellationToken ct = default);
}
