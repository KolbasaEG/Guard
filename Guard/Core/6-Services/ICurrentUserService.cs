namespace Guard.Core.Services;

public interface ICurrentUserService
{
  /// <summary>
  /// Id текущего пользователя (из Claims)
  /// </summary>
  string? UserId { get; }

  /// <summary>
  /// Имя текущего пользователя
  /// </summary>
  string? UserName { get; }
}