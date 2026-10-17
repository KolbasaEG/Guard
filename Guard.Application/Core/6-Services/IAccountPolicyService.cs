using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

public record AccountState(bool Blocked, bool PasswordExpired);

public interface IAccountPolicyService
{
  Task<AccountLoginPolicyDto> GetLoginPolicyAsync(CancellationToken ct = default);
  Task<AccountPasswordLifetimeDto> GetPasswordLifetimeAsync(CancellationToken ct = default);
  Task<AccountState> CheckAsync(string userId, CancellationToken ct = default);
  Task RecordActivityAsync(string userId, CancellationToken ct = default);
  Task SweepAsync(CancellationToken ct = default);
}

public interface IPasswordRequirementsService
{
  Task<PasswordRequirementsDto> GetRequirementsAsync(CancellationToken ct = default);
}

public interface IAccountPolicyAdministrationService
{
  Task<AccountPolicySettingsDto> GetSettingsAsync(CancellationToken ct = default);
  Task<AccountPolicySettingsDto> SaveAsync(SaveAccountPolicyCommand command, CancellationToken ct = default);
}
