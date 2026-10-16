using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Guard.Core.Services.Exports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class SubdivisionSearchIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory,
    IPermissionService rootPermissions, IPermissionService userPermissions, Action<bool, string> check)
  {
    SubdivisionService Service(IPermissionService permissions, IDataAccessScopeService? scopes = null) => new(null!, null!, null!,
      NullLogger<SubdivisionService>.Instance, permissions, scopes ?? new DataAccessScopeService(permissions, factory), factory);
    var root = Service(rootPermissions); var user = Service(userPermissions);
    await using var db = await factory.CreateDbContextAsync();
    var own = await db.Subdivisions.SingleAsync(s => s.Name == "Own");
    var other = await db.Subdivisions.SingleAsync(s => s.Name == "Other");
    var rootUser = await db.Users.SingleAsync(u => u.Id == "root"); var originalPersonal = rootUser.PersonalId;
    var employee = new Personal { FirstName = "Search", LastName = "Root", SubdivisionId = own.Id, CreatedBy = "test" };
    var rows = Enumerable.Range(1, 30).Select(i => new Subdivision {
      Name = "Search " + (i % 2 == 0 ? "Alpha " : "Beta ") + i, ParentId = i <= 24 ? own.Id : other.Id,
      StaffCount = i * 1.5, LevelOrder = i, IsDepartment = i % 2 == 0,
      Phone = i % 3 == 0 ? null : i % 3 == 1 ? "" : "ALPHA", CreatedBy = "test",
      Status = Enum.GetValues<Status>()[(i - 1) % 6], InsertedDate = new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc)
    }).ToArray();
    db.AddRange(rows); await db.SaveChangesAsync();
    foreach (var row in rows) row.Path = (row.ParentId == own.Id ? own.Path : other.Path) + row.SubdivisionId + "/";
    await db.SaveChangesAsync();
    try {
      check((await root.SearchAsync(new())).Count == 0, "Root own-subdivision search is empty without employee");
      db.Add(employee); rootUser.PersonalId = employee.Id; await db.SaveChangesAsync();
      foreach (var mode in Enum.GetValues<DataViewMode>()) {
        var request = new SubdivisionSearchRequest(Mode: mode, AllSubdivisions: true, Take: 100, OrderBy: "Name asc");
        var result = await root.SearchAsync(request);
        var exported = await ExportQueries.Subdivisions(db, new(ExportArea.Subdivisions, ExportFormat.Excel, mode,
          AllSubdivisions: true, OrderBy: request.OrderBy), new(new("root", true, []), own.Id, own.Path, [], "test"))
          .Select(s => s.Id).ToListAsync();
        check(result.Count == exported.Count && result.Items.Select(s => s.Id).SequenceEqual(exported), "subdivision search/export agree for " + mode);
      }
      var current = await root.SearchAsync(new(Take: 100));
      check(current.Items.Count == 1 && current.Items[0].Id == own.Id, "Root own mode narrows full subdivision access");
      var descendants = await user.SearchAsync(new(Hierarchy: SubdivisionHierarchyMode.IncludeChildren, Take: 100));
      var expected = await ExportQueries.Statuses(db.Subdivisions.Where(s => s.Path.StartsWith(own.Path)), DataViewMode.Active).CountAsync();
      check(descendants.Count == expected && descendants.Items.All(s => !rows.Skip(24).Any(p => p.Id == s.Id)), "operator subdivision search contains authorized descendants only");
      check(descendants.Items.Any(s => rows.Any(r => r.Id == s.Id && r.Version == s.Version && r.StaffCount == s.StaffCount && s.ParentName == own.Name)),
        "subdivision SQL projection preserves version, numeric values and parent label");
      var pages = new List<Guid>();
      for (var skip = 0; skip < expected; skip += 3) pages.AddRange((await user.SearchAsync(new(Hierarchy: SubdivisionHierarchyMode.IncludeChildren, Skip: skip, Take: 3))).Items.Select(s => s.Id));
      check(pages.Count == expected && pages.Distinct().Count() == expected, "subdivision pages have stable unique ordering");
      var filters = new List<ExportFilter>();
      foreach (var field in new[] { "Name", "Phone", "Fax", "Parent.Name", "OrganType.Value", "StatusClassifier.Value" })
      foreach (var op in new[] { "Equals", "NotEquals", "Contains", "DoesNotContain", "StartsWith", "EndsWith", "IsNull", "IsNotNull", "IsEmpty", "IsNotEmpty", "In", "NotIn" })
        filters.Add(new(field, op, op is "In" or "NotIn" ? JsonSerializer.SerializeToElement(new[] { "ALPHA", "Own" }) : JsonSerializer.SerializeToElement("Alpha")));
      foreach (var (field, value) in new (string, object)[] { ("StaffCount", 12.0), ("LevelOrder", 12), ("InsertedDate", rows[0].InsertedDate), ("IsDepartment", true), ("Id", rows[0].Id) })
      foreach (var op in field is "IsDepartment" or "Id" ? new[] { "Equals", "NotEquals" } : new[] { "Equals", "NotEquals", "GreaterThan", "GreaterThanOrEquals", "LessThan", "LessThanOrEquals" })
        filters.Add(new(field, op, JsonSerializer.SerializeToElement(value, value.GetType())));
      filters.Add(new(All: false, Children: [new("Phone", "IsNull"), new("Name", "Contains", JsonSerializer.SerializeToElement("Alpha"))]));
      foreach (var filter in filters) {
        var legacy = await ExportFilters.Apply(db.Subdivisions.AsNoTracking(), filter, ExportQueries.SubdivisionFields).OrderBy(s => s.Id).Select(s => s.Id).ToListAsync();
        var actual = await SubdivisionSearchQuery.Filter(db.Subdivisions.AsNoTracking(), filter).OrderBy(s => s.Id).Select(s => s.Id).ToListAsync();
        check(legacy.SequenceEqual(actual), "subdivision PostgreSQL predicate preserves semantics: " + filter.Field + " " + filter.Operation);
      }
      foreach (var field in SubdivisionSearchQuery.Fields) {
        var order = field + " desc";
        var legacy = await ExportQueries.Sort(db.Subdivisions, order, ExportQueries.SubdivisionFields).Select(s => s.Id).ToListAsync();
        var actual = await SubdivisionSearchQuery.Sort(db.Subdivisions, order).Select(s => s.Id).ToListAsync();
        check(legacy.SequenceEqual(actual), "subdivision typed sort preserves ordering: " + field);
      }
      var broad = new ExportFilter(All: false, Children: [new("Name", "Contains", JsonSerializer.SerializeToElement("Search")), new("Phone", "IsNull")]);
      check((await user.SearchAsync(new(Hierarchy: SubdivisionHierarchyMode.IncludeChildren, Take: 100, Filter: broad))).Items.All(s => descendants.Items.Any(d => d.Id == s.Id)), "OR subdivision filter cannot expand authorized scope");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => user.SearchAsync(new(AllSubdivisions: true)), check, "operator cannot request all subdivisions");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => user.SearchAsync(new(Mode: DataViewMode.Deleted)), check, "operator cannot request deleted subdivisions");
      foreach (var request in new[] { new SubdivisionSearchRequest(Skip: -1), new(Take: 101), new(Mode: (DataViewMode)99), new(Hierarchy: (SubdivisionHierarchyMode)99), new(OrderBy: "Personals.Count desc") })
        await PostgresTests.ThrowsAsync<ArgumentException>(() => root.SearchAsync(request), check, "subdivision search validates request on server");
      var limited = new PermissionStub(new("user", false, [Permissions.Subdivisions.Read]));
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => Service(limited).SearchAsync(new(Mode: DataViewMode.Archived)), check, "subdivision archive needs explicit permission");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => Service(new PermissionStub(new("user", false, []))).SearchAsync(new()), check, "subdivision search requires read permission");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => Service(rootPermissions, new ChangingScope()).SearchAsync(new(AllSubdivisions: true)), check, "subdivision search rejects changed scope before returning");
      using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => root.SearchAsync(new(AllSubdivisions: true), cancelled.Token), check, "subdivision search propagates cancellation");
      var originalPath = own.Path;
      try { own.Path = "/invalid/"; await db.SaveChangesAsync();
        check((await root.SearchAsync(new(Hierarchy: SubdivisionHierarchyMode.IncludeChildren))).Count == 0 &&
          (await user.SearchAsync(new(Hierarchy: SubdivisionHierarchyMode.IncludeChildren))).Count == 0, "invalid stored subdivision path yields empty own scope");
      } finally { own.Path = originalPath; await db.SaveChangesAsync(); }
    }
    finally {
      rootUser.PersonalId = originalPersonal; db.Remove(employee); db.RemoveRange(rows); await db.SaveChangesAsync();
    }
  }

  private sealed class ChangingScope : IDataAccessScopeService
  {
    private int calls;
    public Task<DataAccessScope> GetAsync(CancellationToken ct = default) => Task.FromResult(new DataAccessScope("root", ++calls == 1, []));
  }
  private sealed class PermissionStub(UserAccessSnapshot snapshot) : IPermissionService
  {
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(snapshot);
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string code, CancellationToken ct = default) => Task.FromResult(snapshot.Has(code));
    public Task RequireAsync(string code, CancellationToken ct = default) => snapshot.Has(code) ? Task.CompletedTask : throw new UnauthorizedAccessException();
  }
}
