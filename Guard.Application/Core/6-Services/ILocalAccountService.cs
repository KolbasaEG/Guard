using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

// Операции HTTP-запроса: cookie не изменяется из интерактивного circuit.
public interface ILocalAccountService
{
  Task<LocalSignInStatus> SignInAsync(string email, string password, bool rememberMe,
    CancellationToken ct = default);
  Task SignOutAsync(CancellationToken ct = default);
}
