using Guard.Core.Enums;
using Guard.Core.Identity;

internal static class IpStatusTests
{
  public static void Run(Action<bool, string> check)
  {
    // IP has its own legacy policy: restoring Deleted does not take a Root argument.
    var expected = new Dictionary<(string, Status), Status> {
      [("delete", Status.Inserted)] = Status.Deleted,
      [("delete", Status.Modified)] = Status.Deleted,
      [("delete", Status.Archived)] = Status.Deleted,
      [("archive", Status.Inserted)] = Status.Archived,
      [("archive", Status.Modified)] = Status.Archived,
      [("restore", Status.Archived)] = Status.Modified,
      [("restore", Status.Deleted)] = Status.Modified,
      [("block", Status.Inserted)] = Status.Blocked,
      [("block", Status.Modified)] = Status.Blocked,
      [("block", Status.Archived)] = Status.ArchivedBlocked,
      [("unblock", Status.Blocked)] = Status.Modified,
      [("unblock", Status.ArchivedBlocked)] = Status.Archived
    };
    foreach (var status in Enum.GetValues<Status>().Append((Status)0).Append((Status)999))
      foreach (var operation in new[] { "delete", "archive", "restore", "block", "unblock", "", "Restore", "unknown" })
      {
        var allowed = expected.TryGetValue((operation, status), out var next);
        try
        {
          check(IpStatusTransitions.Apply(status, operation) == next && allowed, $"IP transition {status}/{operation}");
        }
        catch (InvalidOperationException) { check(!allowed, $"IP rejected {status}/{operation}"); }
      }
  }
}
