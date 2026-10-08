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
    var registrations = new ServiceCollection();
    registrations.AddLogging(); registrations.AddSingleton(factory);
    registrations.AddScoped<IPermissionService, RootPermissions>();
    registrations.AddScoped<IDataAccessScopeService, RootScope>();
    registrations.AddScoped(typeof(IReadRepository<>), typeof(ReadRepository<>));
    registrations.AddScoped<IUnitOfWorkFactory, UnitOfWorkFactory>();
    registrations.AddScoped<IPersonalService, PersonalService>();
    registrations.AddScoped<ISubdivisionService, SubdivisionService>();
    registrations.AddScoped<IOrganTypeService, OrganTypeService>();
    await using var provider = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    await using var scope = provider.CreateAsyncScope();
    var writes = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();
    var people = scope.ServiceProvider.GetRequiredService<IPersonalService>();
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
    await subdivisions.BlockAsync(parent.Id, expectedVersion: parent.Version);
    await PostgresTests.ThrowsAsync<EntityRuleException>(() => subdivisions.ArchiveAsync(parent.Id), check, "blocked subdivision cannot be archived");
    await using (var db = await factory.CreateDbContextAsync()) {
      check((await db.Subdivisions.FindAsync(child.Id))!.Status == Status.Inserted &&
        (await db.Personals.FindAsync(personal.Id))!.Status == Status.Inserted, "subdivision status does not cascade to descendants or employees");
    }
    await subdivisions.UnblockAsync(parent.Id);
    var original = await people.GetByIdAsync(personal.Id) ?? throw new Exception("missing test employee");
    await people.UpdateFromDtoAsync(new(original.Id, original.Version, PersonalFieldsDto.From(original) with { FirstName = "Changed" }));
    await PostgresTests.ThrowsAsync<EntityConflictException>(() => people.UpdateFromDtoAsync(
      new(original.Id, original.Version, PersonalFieldsDto.From(original))), check, "stale command cannot overwrite newer employee fields");
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
    await people.ArchiveAsync(personal.Id); await people.BlockAsync(personal.Id);
    await PostgresTests.ThrowsAsync<EntityRuleException>(() => people.RestoreAsync(personal.Id), check, "archived blocked employee must be unblocked before restore");
    await people.UnblockAsync(personal.Id); await people.SoftDeleteAsync(personal.Id); await people.RestoreAsync(personal.Id);
    check((await people.GetByIdAsync(personal.Id))!.Status == Status.Modified, "Root restores deleted employee through server operation");
    var organs = scope.ServiceProvider.GetRequiredService<IOrganTypeService>();
    var firstId = await organs.CreateFromDtoAsync(new(new() { ClassifierType = 906, Code = 99001, Name = "First test organ" }));
    var secondId = await organs.CreateFromDtoAsync(new(new() { ClassifierType = 906, Code = 99002, Name = "Second test organ" }));
    check(firstId > 0 && secondId > firstId, "successive organ type creations receive generated IDs");
    await organs.DeleteAsync(firstId); await organs.DeleteAsync(secondId);
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
