namespace Guard.Core.Services.DTOs;

public enum OwnPasswordState { MissingUser, NoPassword, Ready }

public enum OwnPasswordChangeStatus { Succeeded, Unauthorized, Rejected, SignInRequired }

public sealed record OwnPasswordChangeResult(OwnPasswordChangeStatus Status, string? Error = null)
{
  public const string SignInRequiredMessage = "Пароль изменён. Не удалось обновить сеанс. Войдите заново с новым паролем.";
}
