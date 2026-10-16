namespace Guard.Core.Services.DTOs;

public sealed class EditUserDto
{
  public string Id { get; set; } = "";
  public string UserName { get; set; } = "";
  public string Email { get; set; } = "";
  public Guid? PersonalId { get; set; }
  public string Version { get; set; } = "";
}

public sealed record UserEditData(EditUserDto User, IReadOnlyList<UserPersonalOption> Personals, bool IsRoot);
