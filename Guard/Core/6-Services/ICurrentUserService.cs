/// <summary>Идентификатор автора для существующего аудита, полученный на сервере.</summary>
public interface ICurrentUserService
{
  string? UserId { get; }
  string? UserName { get; }
}
