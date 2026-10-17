using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

public interface IOwnPasswordService
{
  Task<OwnPasswordState> GetStateAsync(CancellationToken ct = default);
  Task<OwnPasswordChangeResult> ChangeAsync(string currentPassword, string newPassword,
    string confirmation, CancellationToken ct = default);
}
