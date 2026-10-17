using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class PersonalEditorIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    var own = new Subdivision { Name = "Editor own", Path = "/90001/", CreatedBy = "test" };
    var foreign = new Subdivision { Name = "Editor foreign", Path = "/90002/", CreatedBy = "test" };
    var archived = new Subdivision { Name = "Editor archive", Path = "/90003/", Status = Status.Archived, CreatedBy = "test" };
    var person = new Personal { FirstName = "Editor", LastName = "Projection", SubdivisionId = own.Id,
      PersonalNumber = "editor-number", CreatedBy = "test" };
    var outsider = new Personal { FirstName = "Outside", LastName = "Editor", SubdivisionId = foreign.Id, CreatedBy = "test" };
    var blocked = new Personal { FirstName = "Blocked", LastName = "Editor", SubdivisionId = own.Id,
      Status = Status.Blocked, CreatedBy = "test" };
    await using (var db = await factory.CreateDbContextAsync())
    {
      db.AddRange(own, foreign, archived, person, outsider, blocked);
      await db.SaveChangesAsync();
    }
    try
    {
      var permissions = new Access();
      var scopes = new Scope(own.Id, permissions);
      var service = new PersonalService(new ReadRepository<Personal>(factory, permissions, scopes), null!, null!,
        NullLogger<PersonalService>.Instance, permissions, scopes, factory);
      var result = await service.GetEditorAsync(person.Id);
      check(result != null && result.Id == person.Id && result.Version == person.Version &&
        result.PersonalNumber == person.PersonalNumber && result.ToFields() == EntityWriteMapping.From(person),
        "personal editor SQL projection preserves editable fields and concurrency version");
      check(await service.GetEditorAsync(outsider.Id) == null, "personal editor cannot read foreign employee");
      check(await service.GetEditorAsync(Guid.NewGuid()) == null, "personal editor returns missing employee without graph");
      await PostgresTests.ThrowsAsync<EntityRuleException>(() => service.GetEditorAsync(blocked.Id), check,
        "personal editor rejects blocked employee on server");
      var options = await service.GetSubdivisionOptionsAsync();
      check(options.Count == 1 && options[0].Id == own.Id, "personal editor options respect server scope");
      permissions.Snapshot = new("editor", false, [Permissions.Personals.Read, Permissions.Personals.Write]);
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetEditorAsync(person.Id), check,
        "write permission cannot bypass personal read-details permission");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetSubdivisionOptionsAsync(), check,
        "incomplete write grant cannot bypass read-details dependency for editor options");
      permissions.Snapshot = new("editor", false, [Permissions.Personals.Read, Permissions.Personals.ReadDetails]);
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetEditorAsync(person.Id), check,
        "personal read-details cannot open editor without write");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetSubdivisionOptionsAsync(), check,
        "personal editor options require write on server");
      permissions.Snapshot = new("editor", true, []);
      options = await service.GetSubdivisionOptionsAsync();
      check(options.Any(s => s.Id == foreign.Id) && options.All(s => s.Id != archived.Id),
        "Root editor options include foreign active subdivisions and exclude archive");
      permissions.Snapshot = new("editor", false, [Permissions.Personals.Read, Permissions.Personals.Write, Permissions.Personals.ReadDetails]);
      scopes.Calls = 0;
      scopes.RevokeAfterFirst = true;
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetEditorAsync(person.Id), check,
        "personal editor rejects result after scope revocation during read");
      scopes.Calls = 0;
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetSubdivisionOptionsAsync(), check,
        "personal editor options reject result after scope revocation");
      scopes.RevokeAfterFirst = false;
      permissions.Reads = 0;
      permissions.RevokeAfterRead = 2;
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetEditorAsync(person.Id), check,
        "personal editor rechecks permissions after materialization");
      permissions.Reads = 0;
      permissions.RevokeAfterRead = 1;
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetSubdivisionOptionsAsync(), check,
        "personal editor options recheck permissions after materialization");
      using var cancelled = new CancellationTokenSource();
      cancelled.Cancel();
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => service.GetEditorAsync(person.Id, cancelled.Token), check,
        "personal editor propagates cancellation");
    }
    finally
    {
      await using var db = await factory.CreateDbContextAsync();
      await db.Personals.Where(p => p.Id == person.Id || p.Id == outsider.Id || p.Id == blocked.Id).ExecuteDeleteAsync();
      await db.Subdivisions.Where(s => s.Id == own.Id || s.Id == foreign.Id || s.Id == archived.Id).ExecuteDeleteAsync();
    }
  }

  private sealed class Access : IPermissionService
  {
    public UserAccessSnapshot Snapshot = new("editor", false, [Permissions.Personals.Read, Permissions.Personals.Write, Permissions.Personals.ReadDetails]);
    public int Reads;
    public int RevokeAfterRead = int.MaxValue;
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) =>
      Task.FromResult(++Reads > RevokeAfterRead ? new UserAccessSnapshot("editor", false, []) : Snapshot);
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string code, CancellationToken ct = default) => Task.FromResult(Snapshot.Has(code));
    public Task RequireAsync(string code, CancellationToken ct = default)
    {
      ct.ThrowIfCancellationRequested();
      if (!Snapshot.Has(code)) throw new UnauthorizedAccessException();
      return Task.CompletedTask;
    }
  }

  private sealed class Scope(Guid ownId, Access access) : IDataAccessScopeService
  {
    public int Calls;
    public bool RevokeAfterFirst;
    public Task<DataAccessScope> GetAsync(CancellationToken ct = default) =>
      Task.FromResult(new DataAccessScope("editor", access.Snapshot.IsRoot, ++Calls > 1 && RevokeAfterFirst ? [] : [ownId]));
  }
}
