namespace Guard.Core.Identity;

public static class Permissions
{
  public static class Personals
  {
    public const string Read = "Permissions.Personals.Read";
    public const string ReadDetails = "Permissions.Personals.ReadDetails";
    public const string ReadArchive = "Permissions.Personals.ReadArchive";
    public const string Export = "Permissions.Personals.Export";
    public const string Write = "Permissions.Personals.Write";
  }
  public static class Subdivisions
  {
    public const string Read = "Permissions.Subdivisions.Read";
    public const string ReadArchive = "Permissions.Subdivisions.ReadArchive";
    public const string Write = "Permissions.Subdivisions.Write";
  }
  public static class Users
  {
    public const string Read = "Permissions.Users.Read";
    public const string Manage = "Permissions.Users.Manage";
  }
  public static class IpAddresses
  {
    public const string Read = "Permissions.IpAddresses.Read";
    public const string Manage = "Permissions.IpAddresses.Manage";
  }
  public static class Classifiers
  {
    public const string Read = "Permissions.Classifiers.Read";
    public const string Manage = "Permissions.Classifiers.Manage";
  }
  public static class OrganTypes
  {
    public const string Read = "Permissions.OrganTypes.Read";
    public const string Manage = "Permissions.OrganTypes.Manage";
  }
  public static class Logs
  {
    public const string Read = "Permissions.Logs.Read";
    public const string Configure = "Permissions.Logs.Configure";
  }
  public static class Roles
  {
    public const string Read = "Permissions.Roles.Read";
    public const string Manage = "Permissions.Roles.Manage";
  }
  public static List<string> GetAll() => PermissionCatalog.All.Select(p => p.Code).ToList();
}
