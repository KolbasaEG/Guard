using Guard.Core.Enums;
using Guard.Core.Identity;

internal static class EntityStatusTests
{
  public static void Run(Action<bool, string> check)
  {
    var expected = new Dictionary<(StatusOperation, Status), Status> {
      [(StatusOperation.Archive, Status.Inserted)] = Status.Archived,
      [(StatusOperation.Archive, Status.Modified)] = Status.Archived,
      [(StatusOperation.Block, Status.Inserted)] = Status.Blocked,
      [(StatusOperation.Block, Status.Modified)] = Status.Blocked,
      [(StatusOperation.Block, Status.Archived)] = Status.ArchivedBlocked,
      [(StatusOperation.Unblock, Status.Blocked)] = Status.Modified,
      [(StatusOperation.Unblock, Status.ArchivedBlocked)] = Status.Archived,
      [(StatusOperation.Delete, Status.Inserted)] = Status.Deleted,
      [(StatusOperation.Delete, Status.Modified)] = Status.Deleted,
      [(StatusOperation.Delete, Status.Archived)] = Status.Deleted,
      [(StatusOperation.Restore, Status.Archived)] = Status.Modified
    };
    foreach (var root in new[] { false, true })
      foreach (var status in Enum.GetValues<Status>())
        foreach (var operation in Enum.GetValues<StatusOperation>())
        {
          var allowed = expected.TryGetValue((operation, status), out var next);
          if (root && operation == StatusOperation.Restore && status == Status.Deleted) { allowed = true; next = Status.Modified; }
          check(EntityStatusTransitions.CanApply(status, operation, root) == allowed, $"availability {root}/{status}/{operation}");
          try {
            var actual = EntityStatusTransitions.Apply(status, operation, root);
            check(allowed && actual == next, $"transition {root}/{status}/{operation}");
          }
          catch (InvalidOperationException) { check(!allowed, $"rejected {root}/{status}/{operation}"); }
        }
  }
}
