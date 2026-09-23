using Guard.Core.Identity;

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

  /// <summary>
  /// Получить полный контекст пользователя (аккаунт, физ. лицо, подразделение и подчиненные)
  /// </summary>
  Task<UserContext?> GetContextAsync(CancellationToken ct = default);
}