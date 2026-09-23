namespace Guard.Core.Identity
{
  public static class Permissions
  {
    public static class Subdivisions
    {
      public const string Read = "Permissions.Subdivisions.Read";
      public const string Write = "Permissions.Subdivisions.Write";
    }

    public static class Personals
    {
      public const string Read = "Permissions.Personals.Read";
      public const string Write = "Permissions.Personals.Write";
    }

    public static class Users
    {
      public const string Read = "Permissions.Users.Read";
      public const string Manage = "Permissions.Users.Manage";
    }

    public static List<string> GetAll() => new()
  {
    Subdivisions.Read, Subdivisions.Write,
    Personals.Read, Personals.Write,
    Users.Read, Users.Manage
  };
  }
}
