namespace Guard.Components.Pages.Users;

// Только описание разрешённых полей конструктора; модели Identity в Web не передаются.
public sealed class UserFilterFields
{
  public string Id { get; set; } = "";
  public string? UserName { get; set; }
  public string? Email { get; set; }
  public Guid? PersonalId { get; set; }
  public string? AccountBlockReason { get; set; }
  public DateTimeOffset? LastActivityAtUtc { get; set; }
  public bool MustChangePassword { get; set; }
}
