using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

// Контракт форм управления пользователями не зависит от ASP.NET Identity и EF.
public interface IUserAdministrationService
{
  Task<IReadOnlyList<UserPersonalOption>> GetUserPersonalOptionsAsync(CancellationToken ct = default);
  Task<UserEditData> GetUserEditAsync(string id, CancellationToken ct = default);
  Task<string> CreateFromDtoAsync(CreateUserCommand input, CancellationToken ct = default);
  Task SaveDetailsAsync(EditUserDto input, CancellationToken ct = default);
  Task ResetPasswordFromDtoAsync(ResetUserPasswordCommand input, CancellationToken ct = default);
}
