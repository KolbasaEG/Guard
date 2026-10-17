using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class SubdivisionDetailsIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory,
    IPermissionService rootPermissions, IPermissionService userPermissions, Action<bool, string> check)
  {
    SubdivisionService Service(IPermissionService permissions, IDataAccessScopeService? scope = null) => new(null!, null!, null!,
      NullLogger<SubdivisionService>.Instance, permissions, scope ?? new DataAccessScopeService(permissions, new DataAccessScopeReader(factory)), factory);
    var root = Service(rootPermissions); var user = Service(userPermissions);
    await using var db = await factory.CreateDbContextAsync();
    var own = await db.Subdivisions.SingleAsync(s => s.Name == "Own");
    var other = await db.Subdivisions.SingleAsync(s => s.Name == "Other");
    var rows = new[] { Status.Inserted, Status.Blocked, Status.Archived, Status.ArchivedBlocked, Status.Deleted }
      .Select(status => new Subdivision { Name = "DTO " + status, ParentId = own.Id, Status = status,
        StaffCount = 2.5, Fax = "Fax", CreatedBy = "root", ModifiedBy = "user", Phone = "Phone" }).ToArray();
    var organ = new Classifier { Type = 363, Code = 998801, ClassifierName = "DTO", Value = "DTO organ", IsActive = true };
    var inactive = new Classifier { Type = 363, Code = 998802, ClassifierName = "DTO", Value = "DTO inactive", IsActive = false };
    db.AddRange(rows); db.AddRange(organ, inactive); await db.SaveChangesAsync();
    foreach (var row in rows) row.Path = own.Path + row.SubdivisionId + "/";
    rows[0].OrganTypeId = organ.Type; rows[0].OrganTypeCode = organ.Code;
    await db.SaveChangesAsync();
    try {
      var details = (await user.GetDetailsAsync(rows[0].Id))!;
      check(details.Id == rows[0].Id && details.Version == rows[0].Version && details.ParentName == own.Name &&
        details.ParentId == own.Id && details.OrganTypeName == organ.Value && details.Fax == "Fax" && details.StaffCount == 2.5,
        "subdivision card projects scalar fields, relation labels and concurrency version");
      check(details.Info.Id == details.Id && details.Info.InsertedDate == rows[0].InsertedDate,
        "subdivision card includes audit metadata without loading Identity entities");
      check(await user.GetDetailsAsync(other.Id) == null && await root.GetDetailsAsync(other.Id) != null,
        "subdivision details enforce server scope and explicit Root access");
      check(await root.GetDetailsAsync(Guid.NewGuid()) == null, "missing subdivision card returns null");
      var readOnly = Service(new PermissionsStub(new("user", false, [Permissions.Subdivisions.Read])));
      check(await readOnly.GetDetailsAsync(rows[1].Id) != null && await readOnly.GetDetailsAsync(rows[2].Id) == null &&
        await readOnly.GetDetailsAsync(rows[3].Id) == null && await readOnly.GetDetailsAsync(rows[4].Id) == null,
        "read-only subdivision access allows blocked but excludes archive and deleted");
      check(await user.GetDetailsAsync(rows[2].Id) != null && await user.GetDetailsAsync(rows[3].Id) != null &&
        await user.GetDetailsAsync(rows[4].Id) == null && await root.GetDetailsAsync(rows[4].Id) != null,
        "subdivision archive requires separate permission and deleted requires Root");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => readOnly.GetEditorOptionsAsync(), check,
        "subdivision editor options require write permission");
      var denied = Service(new PermissionsStub(new("user", false, [])));
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => denied.GetDetailsAsync(own.Id), check,
        "subdivision details cannot be read without read permission");
      var options = await user.GetEditorOptionsAsync();
      check(options.Parents.Any(p => p.Id == own.Id) && options.Parents.Any(p => p.Id == rows[0].Id) &&
        options.Parents.All(p => p.Id != other.Id && !rows.Skip(1).Any(r => r.Id == p.Id)),
        "subdivision parent DTO options contain authorized active nodes only");
      check(options.OrganTypes.Contains(new(organ.Type, organ.Code, organ.Value)) && !options.OrganTypes.Any(c => c.Code == inactive.Code),
        "subdivision classifier options preserve composite keys and active organ filtering");
      var parents = await root.GetMoveParentOptionsAsync(own.Id);
      check(parents.Any(p => p.Id == other.Id) && parents.All(p => p.Id != own.Id && !rows.Any(r => r.Id == p.Id)),
        "move parent options exclude target and its complete subtree on server");
      await PostgresTests.ThrowsAsync<KeyNotFoundException>(() => user.GetMoveParentOptionsAsync(other.Id), check,
        "move options reject foreign target");
      await PostgresTests.ThrowsAsync<EntityRuleException>(() => root.GetMoveParentOptionsAsync(rows[1].Id), check,
        "move options reject blocked target");
      var path = rows[0].Path;
      rows[0].Path = ""; await db.SaveChangesAsync();
      await PostgresTests.ThrowsAsync<EntityRuleException>(() => root.GetMoveParentOptionsAsync(rows[0].Id), check,
        "move options reject empty path before querying descendants");
      rows[0].Path = path; await db.SaveChangesAsync();
      foreach (var operation in new Func<SubdivisionService, Task>[] {
        s => s.GetDetailsAsync(own.Id), s => s.GetEditorOptionsAsync(), s => s.GetMoveParentOptionsAsync(own.Id) })
        await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => operation(Service(rootPermissions, new ChangingScope())), check,
          "subdivision dialog read rejects a scope change before returning DTOs");
      using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => root.GetDetailsAsync(own.Id, cancelled.Token), check,
        "subdivision details propagate cancellation");
    }
    finally {
      db.RemoveRange(rows); await db.SaveChangesAsync(); db.RemoveRange(organ, inactive); await db.SaveChangesAsync();
    }
  }

  private sealed class ChangingScope : IDataAccessScopeService
  {
    private int calls;
    public Task<DataAccessScope> GetAsync(CancellationToken ct = default) => Task.FromResult(new DataAccessScope("root", true,
      ++calls == 1 ? [] : [Guid.NewGuid()]));
  }
  private sealed class PermissionsStub(UserAccessSnapshot snapshot) : IPermissionService
  {
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(snapshot);
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string code, CancellationToken ct = default) => Task.FromResult(snapshot.Has(code));
    public Task RequireAsync(string code, CancellationToken ct = default) => snapshot.Has(code) ? Task.CompletedTask : throw new UnauthorizedAccessException();
  }
}
