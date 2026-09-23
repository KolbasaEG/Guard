namespace Guard.Core.Services.DTOs;

public class UserDto
{
  public string Id { get; set; } = default!;
  public string UserName { get; set; } = default!;
  public string Email { get; set; } = default!;
  public Guid? PersonalId { get; set; }
  public string? PersonalFullName { get; set; }
  public bool IsLockedOut { get; set; }
  public List<string> Roles { get; set; } = new();
}

public class CreateUserDto
{
  public string UserName { get; set; } = default!;
  public string Email { get; set; } = default!;
  public string Password { get; set; } = default!;
  public Guid? PersonalId { get; set; }
  public List<string> Roles { get; set; } = new();
}

public class UpdateUserDto
{
  public string Id { get; set; } = default!;
  public string UserName { get; set; } = default!;
  public string Email { get; set; } = default!;
  public Guid? PersonalId { get; set; }
  public List<string> Roles { get; set; } = new();
}

public class RoleDto
{
  public string Id { get; set; } = default!;
  public string Name { get; set; } = default!;
}

public class IpAddressDto
{
  public Guid Id { get; set; }
  public string Address { get; set; } = default!;
  public string? Description { get; set; }
  public int Status { get; set; }
}