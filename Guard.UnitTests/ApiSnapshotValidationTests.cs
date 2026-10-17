using System.Text.Json;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;

internal static class ApiSnapshotValidationTests
{
  private static readonly DateTimeOffset Stamp = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

  public static void Run(Action<bool, string> check)
  {
    void Expect(ApiSnapshot snapshot, bool valid, string scenario,
      bool allowMissing = false, Dictionary<long, long?>? parents = null,
      IEnumerable<(int Type, int Code)>? classifiers = null, Dictionary<long, DateTime>? exits = null)
    {
      var before = JsonSerializer.Serialize(snapshot);
      try
      {
        ApiSnapshotValidator.Validate(snapshot, classifiers, parents, allowMissing, exits);
        check(valid, scenario);
      }
      catch (Exception error) when (error is EntityRuleException or ArgumentException) { check(!valid, scenario); }
      check(JsonSerializer.Serialize(snapshot) == before, $"{scenario}: input remains unchanged");
    }

    Expect(new([], [], []), true, "empty snapshot");
    Expect(Snapshot(), true, "valid complete snapshot");
    Expect(Snapshot() with { Classifiers = [Classifier(), Classifier()] }, false, "duplicate classifier key");
    Expect(Snapshot() with { Subdivisions = [Subdivision(1), Subdivision(1)] }, false, "duplicate subdivision ID");
    Expect(Snapshot() with { Personnel = [Person(), Person()] }, false, "duplicate personnel ID");
    foreach (var id in new long[] { -1, 0, 1 })
    {
      Expect(new([], [Subdivision(id)], []), id > 0, $"subdivision ID {id}");
      var person = Person(); person.Id = id;
      Expect(Snapshot() with { Personnel = [person] }, id > 0, $"personnel ID {id}");
      Expect(Snapshot() with { HeadSubdivisionId = id }, id > 0, $"head ID {id}");
    }
    foreach (var active in new[] { -1, 0, 1, 2 })
    {
      var classifier = Classifier(); classifier.IsActive = active;
      Expect(new([classifier], [], []), active is 0 or 1, $"classifier active {active}");
    }
    var missingStamp = Subdivision(1); missingStamp.UpdatedAt = default;
    Expect(new([], [missingStamp], []), false, "subdivision timestamp required");
    var missingPersonStamp = Person(); missingPersonStamp.UpdatedAt = default;
    Expect(Snapshot() with { Personnel = [missingPersonStamp] }, false, "personnel timestamp required");
    var missingClassifierStamp = Classifier(); missingClassifierStamp.UpdatedAt = default;
    Expect(new([missingClassifierStamp], [], []), false, "classifier timestamp required");
    var blankName = Subdivision(1); blankName.Name = " ";
    Expect(new([], [blankName], []), false, "subdivision field validation");
    var longName = Person(); longName.LastName = new string('x', 101);
    Expect(Snapshot() with { Personnel = [longName] }, false, "personnel field validation");
    var padded = Person(); padded.LastName = " Last ";
    Expect(Snapshot() with { Personnel = [padded] }, true, "validation normalizes only temporary entities");

    foreach (var allowMissing in new[] { false, true })
    {
      Expect(new([], [Subdivision(1, 99)], []), allowMissing, "missing parent", allowMissing);
      Expect(new([], [], [Person()]), allowMissing, "missing personnel subdivision", allowMissing);
      var referenced = Person(); referenced.PositionType = 6; referenced.PositionCode = 0;
      Expect(Snapshot() with { Classifiers = [], Personnel = [referenced] }, allowMissing, "missing classifier", allowMissing);
      Expect(Snapshot() with { Classifiers = [], Personnel = [referenced] }, true, "stored classifier code zero", allowMissing,
        classifiers: [(6, 0)]);
      referenced.PositionCode = null;
      Expect(Snapshot() with { Personnel = [referenced] }, false, "incomplete pair always rejected", allowMissing);
      Expect(new([], [Subdivision(1, 1)], []), false, "self parent always rejected", allowMissing);
      Expect(new([], [Subdivision(1, 2), Subdivision(2, 1)], []), false, "response cycle always rejected", allowMissing);
      Expect(new([], [Subdivision(1, 2)], []), false, "stored cycle always rejected", allowMissing,
        parents: new() { [2] = 1 });
      Expect(new([], [], []) { HeadSubdivisionId = 1 }, allowMissing, "missing configured head", allowMissing);
      Expect(new([], [Subdivision(1), Subdivision(2)], []) { HeadSubdivisionId = 1 }, false,
        "known unrelated root rejected", allowMissing);
    }
    Expect(new([], [Subdivision(1, 99), Subdivision(2, 1)], [Person(2)]) { HeadSubdivisionId = 1 },
      true, "configured head may retain external parent");
    Expect(new([], [], [Person(2)]) { HeadSubdivisionId = 1 }, true, "delta uses stored ancestry",
      parents: new() { [1] = 99, [2] = 1 });
    var knownParents = new Dictionary<long, long?> { [1] = 2, [2] = 1 };
    Expect(new([], [Subdivision(1)], [Person(2)]), true, "snapshot parent supersedes stored parent", parents: knownParents);
    check(knownParents[1] == 2 && knownParents[2] == 1, "stored parent input remains unchanged");

    Expect(new([], [], []) { ExitEvents = [new() { PersonalId = 0, EventAt = Stamp }] }, false, "exit ID required");
    Expect(new([], [], []) { ExitEvents = [new() { PersonalId = 1 }] }, false, "exit timestamp required");
    foreach (var ticks in new long[] { -10, -1, 0, 1, 9, 10 })
    {
      var exit = Stamp.AddTicks(ticks);
      Expect(Snapshot() with { ExitEvents = [new() { PersonalId = 10, EventAt = exit }] }, ticks is < 0 or >= 10,
        $"exit collision at PostgreSQL microsecond precision: {ticks}");
    }
    Expect(Snapshot() with { ExitEvents = [new() { PersonalId = 10, EventAt = Stamp.ToOffset(TimeSpan.FromHours(3)) }] },
      false, "exit comparison uses UTC instant");
    var oldExit = new ApiPersonnelExitEvent { PersonalId = 10, EventAt = Stamp.AddDays(-1) };
    Expect(Snapshot() with { ExitEvents = [oldExit] }, false, "newer stored exit wins",
      exits: new() { [10] = Stamp.UtcDateTime });
    Expect(Snapshot() with { ExitEvents = [oldExit, new() { PersonalId = 10, EventAt = Stamp.AddDays(1) }] }, true,
      "latest response exit wins over older stored collision", exits: new() { [10] = Stamp.UtcDateTime });
    Expect(Snapshot() with { ExitEvents = [oldExit, new() { PersonalId = 10, EventAt = Stamp }] }, false,
      "duplicate exits use latest timestamp");
  }

  private static ApiSnapshot Snapshot() => new([Classifier()], [Subdivision(1)], [Person()]);
  private static RemoteClassifierDto Classifier() => new() {
    Type = 6, Code = 0, ClassifierName = "Position", Value = "", IsActive = 1, UpdatedAt = Stamp
  };
  private static ApiSubdivision Subdivision(long id, long? parent = null) => new() {
    Id = id, ParentId = parent, Name = "Subdivision", UpdatedAt = Stamp
  };
  private static ApiPersonal Person(long subdivision = 1) => new() {
    Id = 10, SubdivisionId = subdivision, LastName = "Last", FirstName = "First", UpdatedAt = Stamp
  };
}
