using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

internal static class OperationLifetimeIntegrationTests
{
  // The caller supplies only the runner's isolated temporary database.
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    var tracked = new ObservedFactory(factory);
    var registrations = new ServiceCollection();
    registrations.AddLogging();
    registrations.AddSingleton<IDbContextFactory<ApplicationDbContext>>(tracked);
    registrations.AddScoped<Actor>();
    registrations.AddScoped<IPermissionService>(sp => sp.GetRequiredService<Actor>());
    registrations.AddScoped<IUnitOfWorkFactory, UnitOfWorkFactory>();
    await using var provider = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    await using var scope = provider.CreateAsyncScope();
    var actor = scope.ServiceProvider.GetRequiredService<Actor>();
    var writes = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();
    var started = DateTime.UtcNow;
    var winner = new Personal { LastName = "Lifetime", FirstName = "Committed" };
    var loser = new Personal { LastName = "Lifetime", FirstName = "Rolled back" };
    actor.Id = "lifetime-first";
    var first = await writes.CreateAsync();
    actor.Id = "lifetime-second";
    var second = await writes.CreateAsync();
    actor.Id = "lifetime-changed-before-save";
    check(tracked.Created.Count == 2 && !ReferenceEquals(tracked.Created[0], tracked.Created[1]),
      "one DI scope creates distinct operation contexts");
    var arrived = 0;
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    async Task WriteAsync(IUnitOfWork work, Personal entity, bool rollback)
    {
      await using (work) {
        await work.ExecuteInTransactionAsync(async () => {
          await work.BaseEntityRepository<Personal>().AddAsync(entity, timeout.Token);
          if (Interlocked.Increment(ref arrived) == 2) release.TrySetResult();
          await release.Task.WaitAsync(timeout.Token);
          await work.SaveChangesAsync(timeout.Token);
          if (rollback) throw new OperationCanceledException("Synthetic rollback after SQL save");
        }, timeout.Token);
      }
    }
    await Task.WhenAll(WriteAsync(first, winner, false), PostgresTests.ThrowsAsync<OperationCanceledException>(
      () => WriteAsync(second, loser, true), check, "parallel operation rolls back after its SQL save"));
    await using (var db = await factory.CreateDbContextAsync()) {
      var stored = await db.Personals.SingleAsync(p => p.Id == winner.Id);
      check(stored.CreatedBy == "lifetime-first" && winner.CreatedBy == "lifetime-first" && loser.CreatedBy == "lifetime-second",
        "each operation retains its captured actor despite later scoped actor changes");
      check(stored.InsertedDate.Kind == DateTimeKind.Utc && stored.InsertedDate >= started && stored.InsertedDate <= DateTime.UtcNow,
        "parallel committed operation persists UTC creation audit");
      check(!await db.Personals.AnyAsync(p => p.Id == loser.Id), "rollback of one concurrent operation preserves only the committed operation");
    }
    foreach (var db in tracked.Created)
      await PostgresTests.ThrowsAsync<ObjectDisposedException>(() => { db.Set<Personal>(); return Task.CompletedTask; }, check,
        "each operation disposes its context before the shared DI scope ends");
    actor.Id = "lifetime-third";
    await using (var third = await writes.CreateAsync()) {
      var row = (await third.BaseEntityRepository<Personal>().GetByIdAsync(winner.Id))!;
      row.FirstName = "After rollback";
      await third.SaveChangesAsync();
    }
    await using (var db = await factory.CreateDbContextAsync()) {
      var stored = await db.Personals.SingleAsync(p => p.Id == winner.Id);
      check(stored.FirstName == "After rollback" && stored.CreatedBy == "lifetime-first" && stored.ModifiedBy == "lifetime-third" &&
        stored.LastModifiedDate?.Kind == DateTimeKind.Utc, "same scoped factory remains usable after rollback with fresh context and update actor");
      db.Remove(stored); await db.SaveChangesAsync();
    }
  }

  public static async Task CheckTreeRollbackAsync(IDbContextFactory<ApplicationDbContext> factory,
    IPermissionService permissions, IDataAccessScopeService access, Guid parentId, Guid childId, Action<bool, string> check)
  {
    await using var source = await factory.CreateDbContextAsync();
    var destination = new Subdivision { Name = "Rollback destination", SubdivisionId = 900003, Path = "/900003/", CreatedBy = "seed" };
    source.Add(destination); await source.SaveChangesAsync();
    var ids = new[] { parentId, childId };
    var before = await source.Subdivisions.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
    var failure = new FailAfterTreeSave();
    var contexts = new TreeFactory(new DbContextOptionsBuilder<ApplicationDbContext>()
      .UseNpgsql(source.Database.GetConnectionString()).AddInterceptors(failure).Options);
    var registrations = new ServiceCollection();
    registrations.AddLogging(); registrations.AddSingleton<IDbContextFactory<ApplicationDbContext>>(contexts);
    registrations.AddSingleton(permissions); registrations.AddSingleton(access);
    registrations.AddScoped<IUnitOfWorkFactory, UnitOfWorkFactory>();
    registrations.AddScoped(typeof(IReadRepository<>), typeof(ReadRepository<>));
    registrations.AddScoped<ISubdivisionManagementService, SubdivisionService>();
    await using var provider = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    await using var scope = provider.CreateAsyncScope();
    await PostgresTests.ThrowsAsync<OperationCanceledException>(
      () => scope.ServiceProvider.GetRequiredService<ISubdivisionManagementService>().MoveAsync(parentId, destination.Id), check,
      "tree move propagates cancellation after SQL save before commit");
    check(failure.Rows == 2, "fault injection observes both root and descendant changed before rollback");
    var after = await source.Subdivisions.AsNoTracking().Where(s => ids.Contains(s.Id)).ToListAsync();
    check(after.All(s => s.Path == before[s.Id].Path && s.ParentId == before[s.Id].ParentId && s.Version == before[s.Id].Version &&
      s.ModifiedBy == before[s.Id].ModifiedBy && s.LastModifiedDate == before[s.Id].LastModifiedDate),
      "failed tree move rolls back all paths, parents, versions and audit metadata");
    source.Remove(destination); await source.SaveChangesAsync();
  }

  private sealed class ObservedFactory(IDbContextFactory<ApplicationDbContext> inner) : IDbContextFactory<ApplicationDbContext>
  {
    public List<ApplicationDbContext> Created { get; } = [];
    public ApplicationDbContext CreateDbContext() { var db = inner.CreateDbContext(); Created.Add(db); return db; }
  }
  private sealed class TreeFactory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
  {
    public ApplicationDbContext CreateDbContext() => new(options);
  }
  private sealed class FailAfterTreeSave : SaveChangesInterceptor
  {
    public int Rows { get; private set; }
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
    {
      Rows = result;
      throw new OperationCanceledException("Synthetic cancellation after tree SQL save");
    }
  }
  private sealed class Actor : IPermissionService
  {
    public string Id { get; set; } = "lifetime";
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(new UserAccessSnapshot(Id, true, []));
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => Task.FromResult(new UserAccessSnapshot(id, true, []));
    public Task<bool> HasAsync(string code, CancellationToken ct = default) => Task.FromResult(true);
    public Task RequireAsync(string code, CancellationToken ct = default) => Task.CompletedTask;
  }
}
