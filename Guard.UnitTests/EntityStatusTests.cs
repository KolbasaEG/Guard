using Guard.Core.Enums;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;

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
      foreach (var status in Enum.GetValues<Status>().Append((Status)0).Append((Status)999))
        foreach (var operation in Enum.GetValues<StatusOperation>().Append((StatusOperation)999))
        {
          var allowed = expected.TryGetValue((operation, status), out var next);
          if (root && operation == StatusOperation.Restore && status == Status.Deleted) { allowed = true; next = Status.Modified; }
          check(EntityStatusTransitions.CanApply(status, operation, root) == allowed, $"availability {root}/{status}/{operation}");
          try {
            var actual = EntityStatusTransitions.Apply(status, operation, root);
            check(allowed && actual == next, $"transition {root}/{status}/{operation}");
          }
          catch (EntityRuleException) { check(!allowed, $"rejected {root}/{status}/{operation}"); }
          foreach (var automatic in new[] { false, true })
          {
            BaseEntity[] entities = [new Personal { Status = status, ArchivedBySynchronization = automatic },
              new Subdivision { Status = status, ArchivedBySynchronization = automatic },
              new IpAddress { Status = status }];
            foreach (var entity in entities)
            {
              var scenario = $"manual {entity.GetType().Name}/{root}/{status}/{operation}/{automatic}";
              try
              {
                check(EntityStatusTransitions.ApplyManual(entity, operation, root) == next && allowed, scenario);
              }
              catch (EntityRuleException) { check(!allowed, $"rejected {scenario}"); }
              check(entity.Status == status, $"decision leaves status assignment to writer: {scenario}");
              if (entity is Personal personal)
                check(personal.ArchivedBySynchronization == (!allowed && automatic), $"archive origin: {scenario}");
              if (entity is Subdivision subdivision)
                check(subdivision.ArchivedBySynchronization == (!allowed && automatic), $"archive origin: {scenario}");
            }
          }
        }
  }
}
