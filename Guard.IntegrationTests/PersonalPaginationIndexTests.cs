using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

internal static class PersonalPaginationIndexTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    await using var db = await factory.CreateDbContextAsync();
    var own = new Subdivision { Name = "Pagination own", Path = "/99001/", CreatedBy = "test" };
    var other = new Subdivision { Name = "Pagination other", Path = "/99002/", CreatedBy = "test" };
    var people = Enumerable.Range(0, 90).Select(n => new Personal {
      LastName = "Same", FirstName = "Name", MiddleName = n.ToString(), Status = (Status)(n % 6 + 1),
      SubdivisionId = n % 2 == 0 ? own.Id : other.Id, CreatedBy = "test"
    }).ToArray();
    db.AddRange(own, other); db.Personals.AddRange(people); await db.SaveChangesAsync();
    var access = new Access();
    var scopes = new Scope(own.Id, access);
    var service = new PersonalService(new ReadRepository<Personal>(factory, access, scopes), null!,
      NullLogger<PersonalService>.Instance, access, scopes, factory);
    var requests = new PersonalSearchRequest[] {
      new(AllSubdivisions: true, Take: 10), new(AllSubdivisions: true, Skip: 20, Take: 10),
      new(AllSubdivisions: true, Mode: DataViewMode.Archived),
      new(AllSubdivisions: true, Mode: DataViewMode.Deleted),
      new(AllSubdivisions: true, OrderBy: "LastName desc"),
      new(AllSubdivisions: true, Filter: new("MiddleName", "Equals", "12"))
    };
    try {
      // Check both directions on actual data, then leave the isolated database at the latest schema.
      await db.GetService<IMigrator>().MigrateAsync("20261017120000_AddIpCanonicalKeys");
      var before = new List<PersonalSearchResult>();
      foreach (var request in requests) before.Add(await service.SearchAsync(request));
      access.Root = false;
      var restricted = await service.SearchAsync(new(Take: 100));
      await db.Database.MigrateAsync();
      access.Root = true;
      for (var i = 0; i < requests.Length; i++) {
        var after = await service.SearchAsync(requests[i]);
        check(after.Count == before[i].Count && after.Items.SequenceEqual(before[i].Items),
          "employee index migration preserves DTO, count, status, sorting and page: " + i);
      }
      check(before[0].Count == 45 && before[2].Count == 30 && before[3].Count == 15,
        "employee index preserves explicit active, archived and deleted sets");
      var expected = people.Where(p => p.Status is Status.Inserted or Status.Modified or Status.Blocked)
        .OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ThenBy(p => p.Id).Skip(20).Take(10).Select(p => p.Id);
      check(before[1].Items.Select(p => p.Id).SequenceEqual(expected), "equal names retain stable Id pagination");
      check(!before[0].Items.Select(p => p.Id).Intersect(before[1].Items.Select(p => p.Id)).Any(),
        "employee pages with equal names do not overlap");
      access.Root = false;
      var afterRestricted = await service.SearchAsync(new(Take: 100));
      check(afterRestricted.Count == 30 && afterRestricted.Items.All(p => p.SubdivisionName == own.Name) &&
        afterRestricted.Items.SequenceEqual(restricted.Items), "employee index preserves server subdivision scope");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.SearchAsync(new(AllSubdivisions: true)),
        check, "employee index does not grant Root visibility to ordinary user");
      check(await db.Database.SqlQueryRaw<int>("""
        SELECT count(*)::int AS "Value" FROM pg_indexes
        WHERE tablename = 'Personals' AND indexname = 'IX_Personals_ActiveName'
        """).SingleAsync() == 1, "employee index exists after down/up migration");
    }
    finally {
      await db.Database.MigrateAsync();
      var ids = people.Select(p => p.Id).ToArray();
      await db.Personals.Where(p => ids.Contains(p.Id)).ExecuteDeleteAsync();
      await db.Subdivisions.Where(s => s.Id == own.Id || s.Id == other.Id).ExecuteDeleteAsync();
    }
  }

  private sealed class Access : IPermissionService
  {
    public bool Root = true;
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) =>
      Task.FromResult(new UserAccessSnapshot("pagination", Root, [Permissions.Personals.Read, Permissions.Personals.ReadArchive]));
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string permission, CancellationToken ct = default) => Task.FromResult(true);
    public Task RequireAsync(string permission, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); return Task.CompletedTask; }
  }

  private sealed class Scope(Guid own, Access access) : IDataAccessScopeService
  {
    public Task<DataAccessScope> GetAsync(CancellationToken ct = default) =>
      Task.FromResult(new DataAccessScope("pagination", access.Root, [own]));
  }
}
