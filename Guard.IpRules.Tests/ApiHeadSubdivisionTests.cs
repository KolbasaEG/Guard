using Guard.Core.Contexts;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

internal static class ApiHeadSubdivisionTests
{
  public static void Run(Action<bool, string> check)
  {
    var snapshot = ApiSynchronizationTests.Snapshot() with { HeadSubdivisionId = 100 };
    snapshot.Subdivisions[0].ParentId = 2;
    ApiSnapshotValidator.Validate(snapshot);
    check(snapshot.Subdivisions[0].ParentId == 2, "head boundary validates without losing external parent");
    void Reject(ApiSnapshot input, string scenario)
    {
      try { ApiSnapshotValidator.Validate(input); check(false, scenario); }
      catch (EntityRuleException) { check(true, scenario); }
    }
    Reject(snapshot with { HeadSubdivisionId = null }, "unknown parent remains an error without explicit head");
    Reject(snapshot with { HeadSubdivisionId = 200 }, "wrong configured head does not exempt another missing parent");
    Reject(snapshot with { HeadSubdivisionId = -1 }, "invalid head ID rejected");
    Reject(snapshot with { Subdivisions = snapshot.Subdivisions.Skip(1).ToArray() }, "head must exist in initial full snapshot");
    var stamp = DateTimeOffset.UtcNow;
    Reject(snapshot with { Subdivisions = snapshot.Subdivisions.Concat([new ApiSubdivision { Id = 999, Name = "Other root", UpdatedAt = stamp }]).ToArray() },
      "unrelated tree outside configured head rejected");
    var missingChild = new ApiSnapshot([], [new() { Id = 100, ParentId = 2, Name = "Head", UpdatedAt = stamp },
      new() { Id = 200, ParentId = 999, Name = "Child", UpdatedAt = stamp }], []) { HeadSubdivisionId = 100 };
    Reject(missingChild, "missing inner parent remains an error");
    var cycle = new ApiSnapshot([], [new() { Id = 100, ParentId = 200, Name = "Head", UpdatedAt = stamp },
      new() { Id = 200, ParentId = 100, Name = "Child", UpdatedAt = stamp }], []) { HeadSubdivisionId = 100 };
    Reject(cycle, "configured head does not hide source cycles");
    var self = new ApiSnapshot([], [new() { Id = 100, ParentId = 100, Name = "Head", UpdatedAt = stamp }], []) { HeadSubdivisionId = 100 };
    Reject(self, "head self reference rejected");
    var delta = new ApiSnapshot(snapshot.Classifiers, [], snapshot.Personnel) { HeadSubdivisionId = 100, UpdatedSince = new(1962, 10, 1) };
    ApiSnapshotValidator.Validate(delta, knownParents: new Dictionary<long, long?> { [100] = 2, [200] = 100 });
    check(true, "incremental employee update resolves head against stored tree");
    var issues = ApiDiagnosticAnalyzer.Analyze(snapshot, new Dictionary<long, long?>(), new HashSet<(int, int)>());
    check(!issues.Any(i => i.Code == "MissingParentInResponse"), "diagnostics recognize configured head boundary");
    var wrongHead = ApiDiagnosticAnalyzer.Analyze(snapshot with { HeadSubdivisionId = 999 }, new Dictionary<long, long?>(), new HashSet<(int, int)>());
    check(wrongHead.Any(i => i.Code == "MissingConfiguredHead") && wrongHead.Any(i => i.Code == "MissingParentInResponse"), "diagnostics retain errors for wrong configured head");
    using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
    check(!db.Database.HasPendingModelChanges(), "API head migration snapshot matches runtime model");
    var sql = db.GetService<IMigrator>().GenerateScript("20261009133225_AddSynchronizationUpdatedSince", "20261010130000_AddApiHeadSubdivision");
    check(sql.Contains("ADD \"ApiHeadSubdivisionId\" bigint") && sql.Contains("CK_SynchronizationSettings_ApiHeadSubdivisionId"), "head migration adds nullable bigint with positive-ID constraint");
    check(!sql.Contains("UPDATE \"Subdivisions\"") && !sql.Contains("DELETE FROM"), "head migration leaves imported data untouched");
  }
}
