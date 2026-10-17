using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Guard.Core.Services.Exports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class ClassifierIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    var root = new PermissionsStub(new("root", true, []));
    IClassifierManagementService Service(IPermissionService permissions) => new ClassifierService(new ReadRepository<Classifier>(factory, permissions, new DataAccessScopeService(permissions, new DataAccessScopeReader(factory))),
      new UnitOfWorkFactory(factory, permissions, null!, NullLogger<UnitOfWork>.Instance), NullLogger<ClassifierService>.Instance, permissions);
    var service = Service(root);
    var fields = new ClassifierFieldsDto { Type = 998811, Code = 1, ClassifierName = "DTO test", Value = "Alpha", IsActive = true };
    var ids = new List<int>();
    try {
      ids.Add(await service.CreateFromDtoAsync(new(fields)));
      ids.Add(await service.CreateFromDtoAsync(new(fields with { Code = 2, Value = "Beta", IsActive = false })));
      var filter = new ExportFilter("Type", "Equals", JsonSerializer.SerializeToElement(fields.Type));
      var result = await service.SearchAsync(new(Take: 1, OrderBy: "Value desc", Filter: filter));
      check(result.Count == 2 && result.Items.Single().Id == ids[1], "classifier search projects DTOs and counts before paging, includes inactive records");
      check((await service.SearchAsync(new(Skip: 1, Take: 1, OrderBy: "Value desc", Filter: filter))).Items.Single().Id == ids[0],
        "classifier server pagination returns next ordered row");
      foreach (var field in ClassifierSearchQuery.Fields) {
        var actual = await service.SearchAsync(new(OrderBy: field + " desc", Filter: filter));
        check(actual.Count == 2, "classifier SQL sort supports " + field);
      }
      foreach (var condition in new[] {
        new ExportFilter("Value", "Contains", JsonSerializer.SerializeToElement("ALP")),
        new("IsActive", "Equals", JsonSerializer.SerializeToElement(true)),
        new("Code", "LessThan", JsonSerializer.SerializeToElement(2)),
        new("UpdatedAt", "LessThan", JsonSerializer.SerializeToElement(DateTime.UtcNow.AddDays(1)))
      }) {
        var actual = await service.SearchAsync(new(Filter: new(Children: [filter, condition])));
        check(actual.Items.Any(i => i.Id == ids[0]), "classifier SQL typed filter supports " + condition.Field);
      }
      var card = (await service.GetDetailsAsync(ids[0]))!;
      check(card.ToFields() == fields && card.Version != Guid.Empty && card.UpdatedAt.Kind == DateTimeKind.Utc,
        "classifier card includes editable fields, version and UTC timestamp");
      await service.UpdateFromDtoAsync(new(card.Id, card.Version, card.ToFields() with { Value = "Updated" }));
      await PostgresTests.ThrowsAsync<EntityConflictException>(() => service.UpdateFromDtoAsync(new(card.Id, card.Version, fields)), check,
        "classifier DTO update rejects stale version");
      await PostgresTests.ThrowsAsync<ArgumentException>(() => service.CreateFromDtoAsync(new(fields)), check,
        "classifier server rejects duplicate code without UI precheck");
      var second = (await service.GetDetailsAsync(ids[1]))!;
      await PostgresTests.ThrowsAsync<ArgumentException>(() => service.UpdateFromDtoAsync(new(second.Id, second.Version, second.ToFields() with { Code = 1 })), check,
        "classifier server rejects duplicate code during update");
      var reader = Service(new PermissionsStub(new("user", false, [Permissions.Classifiers.Read])));
      check((await reader.SearchAsync(new(Filter: filter))).Count == 2 && await reader.GetDetailsAsync(ids[1]) != null,
        "classifier dictionary is global and read-only users can inspect inactive records");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => reader.CreateFromDtoAsync(new(fields with { Code = 3 })), check,
        "classifier DTO write requires manage permission");
      foreach (var write in new Func<Task>[] {
        () => reader.UpdateFromDtoAsync(new(second.Id, second.Version, second.ToFields() with { Value = "Denied" })),
        () => reader.SetActiveStatusAsync(second.Id, true, expectedVersion: second.Version),
        () => reader.DeleteAsync(second.Id, expectedVersion: second.Version)
      })
        await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(write, check,
          "classifier management contract rejects read-only actor for every existing-record write");
      check(await service.GetDetailsAsync(second.Id) == second,
        "denied classifier writes preserve fields, activity, version and audit timestamp");
      await service.SetActiveStatusAsync(second.Id, true, expectedVersion: second.Version);
      var active = (await service.GetDetailsAsync(second.Id))!;
      check(active.IsActive && active.Version != second.Version && active.ToFields() == (second.ToFields() with { IsActive = true }),
        "classifier management contract changes activity and version without changing other fields");
      await PostgresTests.ThrowsAsync<EntityConflictException>(() => service.SetActiveStatusAsync(second.Id, false, expectedVersion: second.Version), check,
        "classifier activity rejects stale version");
      await PostgresTests.ThrowsAsync<EntityConflictException>(() => service.DeleteAsync(second.Id, expectedVersion: second.Version), check,
        "classifier deletion rejects stale version");
      check(await service.GetDetailsAsync(second.Id) == active, "stale classifier writes preserve current record");
      await service.DeleteAsync(second.Id, expectedVersion: active.Version);
      check(await service.GetDetailsAsync(second.Id) == null && (await service.SearchAsync(new(Filter: filter))).Count == 1,
        "classifier management contract deletes current version and removes it from search");
      var denied = Service(new PermissionsStub(new("user", false, [])));
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => denied.SearchAsync(new()), check, "classifier search requires read permission");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => denied.GetDetailsAsync(ids[0]), check, "classifier details require read permission");
      foreach (var read in new Func<IClassifierManagementService, Task>[] { s => s.SearchAsync(new()), s => s.GetDetailsAsync(ids[0]) })
        await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => read(Service(new ChangingPermissions())), check,
          "classifier DTO reads reject changed actor before returning");
      foreach (var request in new ClassifierSearchRequest[] { new(Skip: -1), new(Take: 101), new(OrderBy: "Subdivisions.Count desc"),
        new(Filter: new("Version", "Equals", JsonSerializer.SerializeToElement(Guid.NewGuid()))) })
        await PostgresTests.ThrowsAsync<ArgumentException>(() => service.SearchAsync(request), check, "classifier search rejects untrusted request");
      using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => service.SearchAsync(new(), cancelled.Token), check, "classifier search cancellation");
      check(await service.GetDetailsAsync(-1) == null, "missing classifier details return null");
    }
    finally {
      await using var db = await factory.CreateDbContextAsync();
      db.Classifiers.RemoveRange(await db.Classifiers.Where(c => ids.Contains(c.Id)).ToListAsync()); await db.SaveChangesAsync();
    }
  }

  private class PermissionsStub(UserAccessSnapshot snapshot) : IPermissionService
  {
    public virtual Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(snapshot);
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => GetCurrentAsync(ct);
    public async Task<bool> HasAsync(string code, CancellationToken ct = default) => (await GetCurrentAsync(ct)).Has(code);
    public async Task RequireAsync(string code, CancellationToken ct = default) { if (!await HasAsync(code, ct)) throw new UnauthorizedAccessException(); }
  }
  private sealed class ChangingPermissions() : PermissionsStub(new("root", true, []))
  {
    private int calls;
    public override Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(new UserAccessSnapshot(
      ++calls == 1 ? "root" : "another-root", true, []));
  }
}
