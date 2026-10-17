using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Microsoft.EntityFrameworkCore;

internal static class DataAccessScopeReaderIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    var id = "scope-reader-" + Guid.NewGuid().ToString("N");
    var own = new Subdivision { Name = id, Path = "/900001/12/", CreatedBy = "test" };
    var child = new Subdivision { Name = id + " child", Path = own.Path + "3/", ParentId = own.Id, CreatedBy = "test" };
    var sibling = new Subdivision { Name = id + " sibling", Path = "/900001/123/", CreatedBy = "test" };
    var personal = new Personal { FirstName = id, LastName = "Test", SubdivisionId = own.Id, CreatedBy = "test" };
    var reader = new DataAccessScopeReader(factory);
    var permissions = new DataAccessScopeBoundaryTests.Permissions { User = new(id, false, []) };
    var service = new DataAccessScopeService(permissions, reader);
    await using var db = await factory.CreateDbContextAsync();
    db.AddRange(own, child, sibling, personal, new ApplicationUser { Id = id, UserName = id, PersonalId = personal.Id });
    await db.SaveChangesAsync();
    try {
      check(await reader.FindUserPathAsync("missing-" + id) == null, "scope reader returns null for missing account");
      check((await service.GetAsync()).SubdivisionIds.SetEquals([own.Id, child.Id]), "real scope includes own and child, excludes similar sibling prefix");
      foreach (var status in Enum.GetValues<Status>()) {
        await db.Subdivisions.Where(s => s.Id == child.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status));
        check((await service.GetAsync()).SubdivisionIds.Contains(child.Id), "scope remains independent of display status filter");
      }
      await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.PersonalId, (Guid?)null));
      check((await service.GetAsync()).SubdivisionIds.Count == 0, "same scope service observes removed employee binding");
      await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.PersonalId, personal.Id));
      foreach (var path in new[] { "", "/", "/invalid/" }) {
        await db.Subdivisions.Where(s => s.Id == own.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Path, path));
        check((await service.GetAsync()).SubdivisionIds.Count == 0, "stored malformed path never opens full tree");
        await PostgresTests.ThrowsAsync<ArgumentException>(() => reader.GetSubdivisionIdsAsync(path), check, "direct scope port rejects invalid prefix");
      }
      await db.Subdivisions.Where(s => s.Id == own.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Path, sibling.Path));
      check((await service.GetAsync()).SubdivisionIds.SetEquals([own.Id, sibling.Id]), "same reader observes changed stored path without tracking cache");
      using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => reader.FindUserPathAsync(id, cancelled.Token), check, "scope path read honors cancellation");
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => reader.GetSubdivisionIdsAsync(sibling.Path, cancelled.Token), check, "scope descendants read honors cancellation");
    }
    finally {
      await db.Users.Where(u => u.Id == id).ExecuteDeleteAsync();
      await db.Personals.Where(p => p.Id == personal.Id).ExecuteDeleteAsync();
      await db.Subdivisions.Where(s => s.Id == child.Id).ExecuteDeleteAsync();
      await db.Subdivisions.Where(s => s.Id == own.Id || s.Id == sibling.Id).ExecuteDeleteAsync();
    }
  }
}
