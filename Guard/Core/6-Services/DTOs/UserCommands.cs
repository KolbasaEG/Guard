namespace Guard.Core.Services.DTOs;

// Пароли не включаются в автоматически сгенерированный ToString record.
public sealed class CreateUserCommand(string userName, string email, string password, Guid personalId)
{
  public string UserName { get; } = userName;
  public string Email { get; } = email;
  public string Password { get; } = password;
  public Guid PersonalId { get; } = personalId;
}

public sealed class ResetUserPasswordCommand(string userId, string password)
{
  public string UserId { get; } = userId;
  public string Password { get; } = password;
}
