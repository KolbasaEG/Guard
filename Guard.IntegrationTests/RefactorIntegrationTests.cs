using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class RefactorIntegrationTests
{
  // Invoked only by the existing harness against its own temporary database.
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    await OperationLifetimeIntegrationTests.RunAsync(factory, check);
    await TreeConcurrencyIntegrationTests.RunAsync(factory, new RootPermissions(), new RootScope(), check);
    await ManagementModeConcurrencyTests.RunAsync(factory, new RootPermissions(), new RootScope(), check);
    await IdentityRollbackIntegrationTests.RunAsync(factory, new RootPermissions(), new RootScope(), check);
    var registrations = new ServiceCollection();
    registrations.AddLogging(); registrations.AddSingleton(factory);
    registrations.AddScoped<IPermissionService, RootPermissions>();
    registrations.AddScoped<IDataAccessScopeService, RootScope>();
    registrations.AddScoped(typeof(IReadRepository<>), typeof(ReadRepository<>));
    registrations.AddScoped<IUnitOfWorkFactory, UnitOfWorkFactory>();
    registrations.AddScoped<IPersonalEditorService, PersonalService>();
    registrations.AddScoped<IPersonalStatusService, PersonalService>();
    registrations.AddScoped<IPersonalDetailsService, PersonalService>();
    registrations.AddScoped<ISubdivisionService, SubdivisionService>();
    await using var provider = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    await using var scope = provider.CreateAsyncScope();
    var writes = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();
    var people = scope.ServiceProvider.GetRequiredService<IPersonalEditorService>();
    var statuses = scope.ServiceProvider.GetRequiredService<IPersonalStatusService>();
    var details = scope.ServiceProvider.GetRequiredService<IPersonalDetailsService>();
    var subdivisions = scope.ServiceProvider.GetRequiredService<ISubdivisionService>();
    var parent = new Subdivision { Name = "Refactor parent", SubdivisionId = 900001, Path = "/900001/", CreatedBy = "seed" };
    var child = new Subdivision { Name = "Refactor child", SubdivisionId = 900002, Path = "/900001/900002/", ParentId = parent.Id, CreatedBy = "seed" };
    var personal = new Personal { FirstName = "Refactor", LastName = "Test", SubdivisionId = parent.Id, CreatedBy = "seed" };
    await using (var seed = await factory.CreateDbContextAsync()) {
      seed.AddRange(parent, child, personal,
        new Classifier { Type = 906, Code = 99001, ClassifierName = "Test", Value = "First", IsActive = true },
        new Classifier { Type = 906, Code = 99002, ClassifierName = "Test", Value = "Second", IsActive = true });
      await seed.SaveChangesAsync();
    }
    await OperationLifetimeIntegrationTests.CheckTreeRollbackAsync(factory, new RootPermissions(), new RootScope(), parent.Id, child.Id, check);
    await subdivisions.BlockAsync(parent.Id, expectedVersion: parent.Version);
    await PostgresTests.ThrowsAsync<EntityRuleException>(() => subdivisions.ArchiveAsync(parent.Id), check, "blocked subdivision cannot be archived");
    await using (var db = await factory.CreateDbContextAsync()) {
      check((await db.Subdivisions.FindAsync(child.Id))!.Status == Status.Inserted &&
        (await db.Personals.FindAsync(personal.Id))!.Status == Status.Inserted, "subdivision status does not cascade to descendants or employees");
    }
    await subdivisions.UnblockAsync(parent.Id);
    var subdivisionCard = (await subdivisions.GetDetailsAsync(parent.Id))!;
    await subdivisions.UpdateFromDtoAsync(new(subdivisionCard.Id, subdivisionCard.Version,
      subdivisionCard.ToFields() with { Name = "DTO updated parent" }));
    await PostgresTests.ThrowsAsync<EntityConflictException>(() => subdivisions.UpdateFromDtoAsync(
      new(subdivisionCard.Id, subdivisionCard.Version, subdivisionCard.ToFields())), check,
      "subdivision card version prevents overwriting a newer edit");
    await PostgresTests.ThrowsAsync<EntityRuleException>(() => subdivisions.MoveAsync(parent.Id, child.Id), check,
      "subdivision move rejects a parent inside its subtree");
    var childCard = (await subdivisions.GetDetailsAsync(child.Id))!;
    await subdivisions.MoveAsync(child.Id, null, expectedVersion: childCard.Version);
    await PostgresTests.ThrowsAsync<EntityConflictException>(() => subdivisions.MoveAsync(child.Id, parent.Id,
      expectedVersion: childCard.Version), check, "subdivision move rejects stale card version");
    await using (var moved = await factory.CreateDbContextAsync()) {
      var result = await moved.Subdivisions.SingleAsync(s => s.Id == child.Id);
      check(result.ParentId == null && result.Path == $"/{result.SubdivisionId}/",
        "subdivision DTO move preserves agreement of parent and materialized path");
    }
    var original = await people.GetEditorAsync(personal.Id) ?? throw new Exception("missing test employee");
    await people.UpdateFromDtoAsync(new(original.Id, original.Version, original.ToFields() with { FirstName = "Changed" }));
    await PostgresTests.ThrowsAsync<EntityConflictException>(() => people.UpdateFromDtoAsync(
      new(original.Id, original.Version, original.ToFields())), check, "stale command cannot overwrite newer employee fields");
    await using (var first = await writes.CreateAsync())
    await using (var second = await writes.CreateAsync()) {
      var a = (await first.BaseEntityRepository<Personal>().GetByIdAsync(personal.Id))!;
      var b = (await second.BaseEntityRepository<Personal>().GetByIdAsync(personal.Id))!;
      a.FirstName = "Winner"; b.FirstName = "Loser";
      await first.SaveChangesAsync();
      await PostgresTests.ThrowsAsync<EntityConflictException>(() => second.SaveChangesAsync(), check, "EF concurrency prevents a race after both reads");
    }
    var rolledBack = Guid.NewGuid();
    await using (var work = await writes.CreateAsync()) {
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => work.ExecuteInTransactionAsync(async () => {
        await work.BaseEntityRepository<Personal>().AddAsync(new Personal { Id = rolledBack, FirstName = "Rollback", LastName = "Test", SubdivisionId = parent.Id });
        await work.SaveChangesAsync();
        throw new OperationCanceledException("test rollback");
      }), check, "failed compound write rolls back even after save");
    }
    await using (var db = await factory.CreateDbContextAsync()) {
      check(!await db.Personals.AnyAsync(p => p.Id == rolledBack), "rollback leaves no partial record");
      var result = await db.Personals.SingleAsync(p => p.Id == personal.Id);
      check(result.FirstName == "Winner" && result.CreatedBy == "seed" && result.ModifiedBy == "test-root",
        "winning update preserves creation audit and records current actor");
    }
    await statuses.ArchiveAsync(personal.Id); await statuses.BlockAsync(personal.Id);
    await PostgresTests.ThrowsAsync<EntityRuleException>(() => statuses.RestoreAsync(personal.Id), check, "archived blocked employee must be unblocked before restore");
    await statuses.UnblockAsync(personal.Id); await statuses.SoftDeleteAsync(personal.Id); await statuses.RestoreAsync(personal.Id);
    check((await details.GetDetailsAsync(personal.Id))!.Status == Status.Modified, "Root restores deleted employee through server operation");
    await using (var cleanup = await factory.CreateDbContextAsync()) {
      cleanup.Personals.Remove((await cleanup.Personals.FindAsync(personal.Id))!);
      cleanup.Subdivisions.Remove((await cleanup.Subdivisions.FindAsync(child.Id))!);
      await cleanup.SaveChangesAsync();
      cleanup.Subdivisions.Remove((await cleanup.Subdivisions.FindAsync(parent.Id))!);
      cleanup.Classifiers.RemoveRange(await cleanup.Classifiers.Where(c => c.Type == 906 && (c.Code == 99001 || c.Code == 99002)).ToListAsync());
      await cleanup.SaveChangesAsync();
    }
  }
  private sealed class RootPermissions : IPermissionService
  {
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(new UserAccessSnapshot("test-root", true, []));
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string code, CancellationToken ct = default) => Task.FromResult(true);
    public Task RequireAsync(string code, CancellationToken ct = default) => Task.CompletedTask;
  }
  private sealed class RootScope : IDataAccessScopeService
  {
    public Task<DataAccessScope> GetAsync(CancellationToken ct = default) => Task.FromResult(new DataAccessScope("test-root", true, []));
  }
}
