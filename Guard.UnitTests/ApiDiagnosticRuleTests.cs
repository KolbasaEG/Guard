using System.Text.Json;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;

internal static class ApiDiagnosticRuleTests
{
  private static readonly DateTimeOffset Stamp = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

  public static void Run(Action<bool, string> check)
  {
    IReadOnlyList<ApiDiagnosticIssue> Analyze(ApiSnapshot snapshot,
      Dictionary<long, long?>? parents = null, HashSet<(int, int)>? classifiers = null)
    {
      parents ??= []; classifiers ??= [];
      var before = JsonSerializer.Serialize(snapshot);
      var parentCopy = parents.ToArray(); var classifierCopy = classifiers.ToHashSet();
      var issues = ApiDiagnosticAnalyzer.Analyze(snapshot, parents, classifiers);
      check(before == JsonSerializer.Serialize(snapshot) && parentCopy.SequenceEqual(parents) && classifierCopy.SetEquals(classifiers),
        "diagnostic analysis preserves snapshot and stored inputs");
      return issues;
    }
    check(Analyze(new([], [], [])).Count == 0, "empty diagnostic snapshot");
    check(Analyze(Snapshot()).Count == 0, "valid diagnostic snapshot");
    foreach (var ticks in new long[] { -10, -1, 0, 1, 9, 10 })
    {
      var snapshot = Snapshot() with { ExitEvents = [new() { PersonalId = 10, EventAt = Stamp.AddTicks(ticks) }] };
      check(Analyze(snapshot).Any(i => i.Code == "AmbiguousExitState") == (ticks is >= 0 and < 10),
        $"diagnostic exit comparison at microsecond boundary {ticks}");
    }
    check(Analyze(Snapshot() with { ExitEvents = [new() { PersonalId = 10, EventAt = Stamp.ToOffset(TimeSpan.FromHours(3)) }] })
      .Single().Code == "AmbiguousExitState", "diagnostic exit compares UTC instants");
    check(Analyze(Snapshot() with { ExitEvents = [new() { PersonalId = 10, EventAt = Stamp },
      new() { PersonalId = 10, EventAt = Stamp.AddDays(1) }] }).Count == 0, "latest exit removes earlier ambiguity");
    var invalidExit = Analyze(new([], [], []) { ExitEvents = [new() { PersonalId = 0 }] });
    check(invalidExit.Count == 2 && invalidExit.All(i => i.Resource == "personnel/out-of-scope" && i.RecordId == "0") &&
      invalidExit.Select(i => i.Code).ToHashSet().SetEquals(["InvalidId", "MissingDate"]), "invalid exit accumulates ID and date issues");

    foreach (var length in new[] { 100, 101 })
    {
      var snapshot = Snapshot(); snapshot.Personnel[0].LastName = " " + new string('x', length) + " ";
      check(Analyze(snapshot).Any(i => i.Field == "LastName" && i.Code == "TooLong") == (length > 100),
        $"required names checked after trimming: {length}");
      snapshot.Personnel[0].LastName = "Last"; snapshot.Personnel[0].LastNameGen = " " + new string('x', length) + " ";
      check(Analyze(snapshot).Any(i => i.Field == "LastNameGen" && i.Code == "TooLong"), "genitive name retains whitespace in length check");
    }
    foreach (var year in new int?[] { null, 1899, 1900, 2100, 2101 })
    {
      var snapshot = Snapshot(); snapshot.Personnel[0].EnlistmentYear = year;
      check(Analyze(snapshot).Any(i => i.Code == "OutOfRange") == (year is < 1900 or > 2100), $"diagnostic year {year}");
    }
    foreach (var count in new[] { -1d, 0d, .5, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
    {
      var snapshot = Snapshot(); snapshot.Subdivisions[0].StaffCount = count;
      // JSON cannot represent non-finite numbers; preservation is checked through the scalar itself here.
      var issues = ApiDiagnosticAnalyzer.Analyze(snapshot, new Dictionary<long, long?>(), new HashSet<(int, int)>());
      check(issues.Any(i => i.Code == "InvalidNumber") == (!double.IsFinite(count) || count < 0) &&
        snapshot.Subdivisions[0].StaffCount.Equals(count), $"diagnostic staff count {count}");
    }
    var pair = Snapshot(); pair.Personnel[0].PositionType = 6; pair.Personnel[0].PositionCode = 0;
    check(Analyze(pair, classifiers: [(6, 0)]).Single() is { Code: "MissingClassifierInResponse", ReferenceId: "6:0", FoundLocally: true },
      "stored classifier is reported as missing in response with local resolution");
    pair.Personnel[0].PositionCode = null;
    check(Analyze(pair).Single().Code == "IncompletePair", "half classifier pair reported without a missing-reference issue");
    var head = Snapshot() with { HeadSubdivisionId = 1 }; head.Subdivisions[0].ParentId = 99;
    check(Analyze(head).Count == 0, "configured head permits external parent");
    check(Analyze(Snapshot() with { HeadSubdivisionId = 99 }).Any(i => i.Code == "MissingConfiguredHead") &&
      Analyze(Snapshot() with { HeadSubdivisionId = 99 }).Any(i => i.Code == "OutsideConfiguredHead"),
      "missing configured head and known outside tree are both reported");
    var delta = new ApiSnapshot([], [], [Person(2)]) { HeadSubdivisionId = 1 };
    check(Analyze(delta, new() { [1] = null, [2] = 1 }).Single() is { Code: "MissingSubdivisionInResponse", FoundLocally: true },
      "delta ancestry uses stored parents without reporting an outside tree");
    var duplicate = new ApiSnapshot([], [Subdivision(1), Subdivision(1, 2), Subdivision(2, 1)], []);
    var duplicateIssues = Analyze(duplicate);
    check(duplicateIssues.Single().Code == "DuplicateId", "diagnostic graph preserves first duplicate parent");
    var overwrite = new ApiSnapshot([], [Subdivision(1)], []);
    check(Analyze(overwrite, new() { [1] = 2, [2] = 1 }).Count == 0, "response parent overrides stored cycle");
  }

  private static ApiSnapshot Snapshot() => new([], [Subdivision(1)], [Person(1)]);
  private static ApiSubdivision Subdivision(long id, long? parent = null) => new() {
    Id = id, ParentId = parent, Name = "Subdivision", UpdatedAt = Stamp
  };
  private static ApiPersonal Person(long subdivision) => new() {
    Id = 10, SubdivisionId = subdivision, LastName = "Last", FirstName = "First", UpdatedAt = Stamp
  };
}
