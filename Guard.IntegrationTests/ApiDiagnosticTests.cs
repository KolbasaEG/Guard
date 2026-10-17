using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Guard.Core.Identity;
using Microsoft.Extensions.Logging.Abstractions;

internal static class ApiDiagnosticTests
{
  public static async Task CheckAccessAsync(Action<bool, string> check)
  {
    var permissions = new PermissionsStub();
    var service = new ApiDiagnosticService(null!, permissions, null!, null!, NullLogger<ApiDiagnosticService>.Instance);
    try { await service.RunAsync(new(1978, 10, 9)); check(false, "non-Root diagnostics rejected on server"); }
    catch (UnauthorizedAccessException) { check(true, "non-Root diagnostics rejected before database or HTTP"); }
    permissions.Root = true;
    try { await service.RunAsync(default); check(false, "diagnostic service validates mandatory date"); }
    catch (ArgumentException) { check(true, "diagnostic service validates mandatory date before database or HTTP"); }
    try { await service.RunAsync(new(1978, 10, 9)); check(false, "diagnostics require full log level"); }
    catch (EntityRuleException) { check(true, "diagnostics reject log level that suppresses the protocol"); }
  }

  private sealed class PermissionsStub : IPermissionService
  {
    public bool Root { get; set; }
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(new UserAccessSnapshot("diagnostic-test", Root, []));
    public Task<UserAccessSnapshot> GetForUserAsync(string userId, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string permission, CancellationToken ct = default) => Task.FromResult(Root);
    public Task RequireAsync(string permission, CancellationToken ct = default) => Root ? Task.CompletedTask : throw new UnauthorizedAccessException();
  }

  public static void Run(Action<bool, string> check)
  {
    var stamp = DateTimeOffset.UtcNow;
    var snapshot = new ApiSnapshot(
      [new() { Type = 6, Code = 0, ClassifierName = "VALID", Value = "", IsActive = 1, UpdatedAt = stamp }],
      [new() { Id = 1, ParentId = 99, Name = "Sensitive department", UpdatedAt = stamp },
       new() { Id = 2, ParentId = 98, Name = "", StaffCount = -1 },
       new() { Id = 3, ParentId = 4, Name = "A", UpdatedAt = stamp },
       new() { Id = 4, ParentId = 3, Name = "B", UpdatedAt = stamp },
       new() { Id = 5, ParentId = 5, Name = "C", UpdatedAt = stamp },
       new() { Id = 1, Name = "Duplicate", UpdatedAt = stamp }],
      [new() { Id = 10, SubdivisionId = 99, FirstName = "Sensitive name", LastName = "Test", UpdatedAt = stamp, PositionType = 6, PositionCode = 12 },
       new() { Id = 11, SubdivisionId = 98, FirstName = "", LastName = new string('x', 101), StatusType = 6 }]);
    var local = new Dictionary<long, long?> { [99] = null };
    var issues = ApiDiagnosticAnalyzer.Analyze(snapshot, local, new HashSet<(int, int)> { (6, 12) });
    check(issues.Any(i => i.RecordId == "1" && i.ReferenceId == "99" && i.Code == "MissingParentInResponse" && i.FoundLocally), "diagnostics distinguish parent resolvable locally");
    check(issues.Any(i => i.RecordId == "2" && i.ReferenceId == "98" && !i.FoundLocally), "diagnostics identify unknown external parent");
    check(issues.Any(i => i.Code == "DuplicateId") && issues.Count(i => i.Code == "CycleInResponse") == 3,
      "diagnostics collect duplicates, self cycles and all nodes of other cycles");
    check(issues.Any(i => i.Resource == "personnel" && i.RecordId == "10" && i.Code == "MissingSubdivisionInResponse" && i.FoundLocally), "diagnostics resolve employee subdivision against local data");
    check(issues.Any(i => i.Code == "MissingClassifierInResponse" && i.ReferenceId == "6:12" && i.FoundLocally), "diagnostics distinguish stored classifiers");
    check(issues.Any(i => i.Code == "IncompletePair") && issues.Any(i => i.Code == "TooLong") && issues.Any(i => i.Code == "Required") && issues.Any(i => i.Code == "InvalidNumber"),
      "diagnostics accumulate independent field errors on multiple records");
    check(!System.Text.Json.JsonSerializer.Serialize(issues).Contains("Sensitive"), "diagnostic issues never contain personal field values");
    check(!issues.Any(i => i.Resource == "classifiers"), "diagnostics accept classifier code zero and empty value");
    var mergedCycle = new ApiSnapshot([], [new() { Id = 1, ParentId = 99, Name = "A", UpdatedAt = stamp }], []);
    var combined = ApiDiagnosticAnalyzer.Analyze(mergedCycle, new Dictionary<long, long?> { [99] = 1 }, new HashSet<(int, int)>());
    check(combined.Count(i => i.Code == "CycleWithLocalData") == 2 && !combined.Any(i => i.Code == "CycleInResponse"), "diagnostics detect cycles through stored parents");
    using var stop = new CancellationTokenSource(); stop.Cancel();
    try { ApiDiagnosticAnalyzer.Analyze(snapshot, local, new HashSet<(int, int)>(), stop.Token); check(false, "diagnostic analyzer supports cancellation"); }
    catch (OperationCanceledException) { check(true, "diagnostic analyzer supports cancellation"); }
    var longTree = Enumerable.Range(1, 10000).Select(id => new ApiSubdivision { Id = id, ParentId = id == 1 ? null : id - 1, Name = "A", UpdatedAt = stamp }).ToArray();
    check(ApiDiagnosticAnalyzer.Analyze(new([], longTree, []), new Dictionary<long, long?>(), new HashSet<(int, int)>()).Count == 0,
      "diagnostic graph traversal handles deep hierarchy without recursion");
  }
}
