using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Guard.Core.Services.Exports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class IpSearchIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory,
    IIpAddressManagementService root, IIpAddressManagementService user, Action<bool, string> check)
  {
    await using var db = await factory.CreateDbContextAsync();
    var own = await db.Subdivisions.SingleAsync(s => s.Name == "Own");
    var originalPath = own.Path;
    var other = await db.Subdivisions.SingleAsync(s => s.Name == "Other");
    var child = new Subdivision { Name = "IP search child", ParentId = own.Id, CreatedBy = "test" };
    db.Add(child); await db.SaveChangesAsync();
    child.Path = own.Path + child.SubdivisionId + "/";
    var rows = Enumerable.Range(1, 30).Select(i => new IpAddress {
      Address = $"2001:db8:feed::{i:x}", Description = i % 3 == 0 ? null : i % 3 == 1 ? "" : "Search Alpha",
      SubdivisionId = i <= 24 ? own.Id : i <= 27 ? child.Id : other.Id,
      CreatedBy = "test", InsertedDate = new DateTime(2026, 10, 16, 0, 0, 0, DateTimeKind.Utc)
    }).ToArray();
    db.AddRange(rows); await db.SaveChangesAsync();
    var rootUser = await db.Users.SingleAsync(u => u.Id == "root");
    var originalPersonalId = rootUser.PersonalId;
    var rootPersonal = new Personal { LastName = "IP search root", FirstName = "Test", SubdivisionId = own.Id, CreatedBy = "test" };
    try {
      check((await root.SearchAsync(new())).Count == 0, "Root own-IP search is empty without employee subdivision");
      db.Add(rootPersonal); rootUser.PersonalId = rootPersonal.Id; await db.SaveChangesAsync();
      foreach (var mode in Enum.GetValues<DataViewMode>()) {
        var request = new IpSearchRequest(Mode: mode, AllSubdivisions: true, OrderBy: "Address asc", Take: 100);
        var result = await root.SearchAsync(request);
        var exported = await ExportQueries.IpAddresses(db,
          new(ExportArea.IpAddresses, ExportFormat.Excel, mode, AllSubdivisions: true, OrderBy: request.OrderBy),
          new(new("root", true, []), own.Id, own.Path, [], "test")).Select(p => p.Id).ToListAsync();
        check(result.Count == exported.Count && result.Items.Select(p => p.Id).SequenceEqual(exported),
          "IP SearchAsync and export share ordering and status semantics");
      }
      foreach (var hierarchy in Enum.GetValues<SubdivisionHierarchyMode>()) {
        var result = await root.SearchAsync(new(Hierarchy: hierarchy, Take: 100));
        var expected = await IpSearchQuery.ApplyVisibility(db.IpAddresses, DataViewMode.Active, false, true, own.Path, hierarchy).CountAsync();
        check(result.Count == expected && !result.Items.Any(p => rows.Skip(27).Any(f => f.Id == p.Id)),
          "Root own-IP search uses stored employee path and selected hierarchy");
      }
      var descendants = await user.SearchAsync(new(Hierarchy: SubdivisionHierarchyMode.IncludeChildren, Take: 100));
      check(rows.Take(27).All(p => descendants.Items.Any(item => item.Id == p.Id)) &&
        !descendants.Items.Any(p => rows.Skip(27).Any(f => f.Id == p.Id)), "operator search includes authorized descendants and excludes foreign IP");
      var pages = new List<Guid>();
      for (var skip = 0; skip < 30; skip += 10) pages.AddRange((await root.SearchAsync(new(AllSubdivisions: true, Skip: skip, Take: 10))).Items.Select(p => p.Id));
      check(pages.Count == pages.Distinct().Count(), "IP default sort has stable non-overlapping pages with identical timestamps");
      var ownResult = await user.SearchAsync(new(Take: 100));
      var allFilter = new ExportFilter(All: false, Children: [
        new("Address", "StartsWith", JsonSerializer.SerializeToElement("2001:")),
        new("Description", "IsNull")]);
      var narrowed = await user.SearchAsync(new(Take: 100, Filter: allFilter));
      check(narrowed.Items.All(item => ownResult.Items.Any(p => p.Id == item.Id)), "OR filter cannot expand server IP scope");
      foreach (var field in new[] { "Description", "Subdivision.Name" })
      foreach (var operation in new[] { "Equals", "NotEquals", "Contains", "DoesNotContain", "IsNull", "IsNotNull", "IsEmpty", "IsNotEmpty", "In", "NotIn" }) {
        var filter = new ExportFilter(field, operation, operation is "In" or "NotIn"
          ? JsonSerializer.SerializeToElement(new[] { "Search Alpha" }) : JsonSerializer.SerializeToElement("Alpha"));
        var expected = await ExportFilters.Apply(db.IpAddresses.AsNoTracking(), filter, ExportQueries.IpFields).OrderBy(p => p.Id).Select(p => p.Id).ToListAsync();
        var actual = await IpSearchQuery.Filter(db.IpAddresses.AsNoTracking(), filter).OrderBy(p => p.Id).Select(p => p.Id).ToListAsync();
        check(expected.SequenceEqual(actual), "new IP predicate preserves PostgreSQL null/case semantics: " + field + " " + operation);
      }
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => user.SearchAsync(new(AllSubdivisions: true)), check, "server IP search rejects non-Root all-records flag");
      foreach (var request in new[] { new IpSearchRequest(Skip: -1), new(Take: 101), new(Mode: (DataViewMode)999),
        new(Hierarchy: (SubdivisionHierarchyMode)999), new(OrderBy: "Personals.Count desc"),
        new(Filter: new("Subdivision.Path", "Contains", JsonSerializer.SerializeToElement("/"))) })
        await PostgresTests.ThrowsAsync<ArgumentException>(() => root.SearchAsync(request), check, "IP search validates requests independently of UI");
      using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => root.SearchAsync(new(AllSubdivisions: true), cancelled.Token), check, "IP search propagates cancellation");
      var changing = new ChangingAccess();
      var changed = new IpAddressService(factory, changing, NullLogger<IpAddressService>.Instance);
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => changed.SearchAsync(new(AllSubdivisions: true)), check, "IP search rejects result when access changes during query");
      own.Path = "/invalid/"; await db.SaveChangesAsync();
      check((await root.SearchAsync(new(Hierarchy: SubdivisionHierarchyMode.IncludeChildren))).Count == 0,
        "invalid path in database yields empty own-IP search even for Root");
    }
    finally {
      own.Path = originalPath;
      rootUser.PersonalId = originalPersonalId;
      db.RemoveRange(rows); db.Remove(rootPersonal); db.Remove(child);
      await db.SaveChangesAsync();
    }
  }

  private sealed class ChangingAccess : IIpManagementAccessService
  {
    private int calls;
    public Task<IpManagementScope> GetScopeAsync(bool write, CancellationToken ct = default) =>
      Task.FromResult(++calls == 1 ? new IpManagementScope("root", true, []) : new IpManagementScope("root", false, []));
  }
}
