using System.Data.Common;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

internal static class TreeConcurrencyIntegrationTests
{
  // Only the PostgreSQL runner's disposable database is supplied here.
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory,
    IPermissionService permissions, IDataAccessScopeService access, Action<bool, string> check)
  {
    foreach (var scenario in new[] { "destination", "ancestor", "stale descendant", "cycle", "create", "cancelled" }) {
      using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
      var ct = timeout.Token;
      using var competingCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
      await using var observer = await factory.CreateDbContextAsync(ct);
      var a = new Subdivision { Name = "Tree A", SubdivisionId = 910001, Path = "/910001/", CreatedBy = "seed" };
      var b = new Subdivision { Name = "Tree B", SubdivisionId = 910002, Path = "/910002/", CreatedBy = "seed" };
      var c = new Subdivision { Name = "Tree C", SubdivisionId = 910003, Path = "/910003/", CreatedBy = "seed" };
      var d = new Subdivision { Name = "Tree D", SubdivisionId = 910006, Path = "/910006/", CreatedBy = "seed" };
      var child = new Subdivision { Name = "Tree child", SubdivisionId = 910004, ParentId = a.Id, Path = a.Path + "910004/", CreatedBy = "seed" };
      if (scenario == "ancestor") { a.ParentId = b.Id; a.Path = b.Path + "910001/"; child.Path = a.Path + "910004/"; }
      observer.AddRange(a, b, c, d, child); await observer.SaveChangesAsync(ct);
      var firstGate = new SaveGate();
      var secondGate = new LockAttempt();
      await using var firstProvider = Provider(firstGate);
      await using var secondProvider = Provider(secondGate);
      await using var firstScope = firstProvider.CreateAsyncScope();
      await using var secondScope = secondProvider.CreateAsyncScope();
      var firstService = firstScope.ServiceProvider.GetRequiredService<ISubdivisionService>();
      var secondService = secondScope.ServiceProvider.GetRequiredService<ISubdivisionService>();
      var first = firstService.MoveAsync(scenario == "ancestor" ? b.Id : a.Id, c.Id, ct);
      Task? second = null;
      try {
        await firstGate.Ready.Task.WaitAsync(ct);
        second = scenario switch {
          "stale descendant" => secondService.MoveAsync(child.Id, b.Id, ct, child.Version),
          "cycle" => secondService.MoveAsync(c.Id, a.Id, ct),
          "create" => secondService.CreateAsync(new Subdivision { Name = "Concurrent child", SubdivisionId = 910005, ParentId = a.Id }, ct),
          "ancestor" => secondService.MoveAsync(d.Id, a.Id, ct),
          "cancelled" => secondService.MoveAsync(b.Id, a.Id, competingCancellation.Token),
          _ => secondService.MoveAsync(b.Id, a.Id, ct)
        };
        var pid = await secondGate.Ready.Task.WaitAsync(ct);
        bool waiting;
        do {
          waiting = await observer.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_locks WHERE pid = {pid} AND locktype = 'advisory' AND NOT granted").SingleAsync(ct) > 0;
          if (!waiting && !second.IsCompleted) await Task.Delay(10, ct);
        } while (!waiting && !second.IsCompleted);
        check(waiting && secondGate.TreeReads == 0, $"{scenario}: competing operation waits in PostgreSQL before reading the tree");
        if (scenario == "cancelled") {
          competingCancellation.Cancel();
          await PostgresTests.ThrowsAsync<OperationCanceledException>(() => second!, check,
            "waiting move can be cancelled while predecessor still holds the tree lock");
        }
      }
      finally { firstGate.Release.TrySetResult(); }
      await first;
      if (scenario == "stale descendant")
        await PostgresTests.ThrowsAsync<EntityConflictException>(() => second!, check, "overlapping move checks descendant version after predecessor commits");
      else if (scenario == "cycle")
        await PostgresTests.ThrowsAsync<EntityRuleException>(() => second!, check, $"{scenario}: cycle check uses committed ancestor paths");
      else if (scenario != "cancelled") await second!;
      var rows = await observer.Subdivisions.AsNoTracking().Where(s => s.SubdivisionId >= 910001 && s.SubdivisionId <= 910006).ToDictionaryAsync(s => s.Id, ct);
      check(rows.Values.All(s => s.Path == (s.ParentId.HasValue ? rows[s.ParentId.Value].Path : "/") + s.SubdivisionId + "/"),
        $"{scenario}: every persisted parent and descendant path agrees after concurrent operations");
      check(rows[a.Id].ModifiedBy == "test-root" && rows[child.Id].ModifiedBy == "test-root" &&
        rows[a.Id].Version != a.Version && rows[child.Id].Version != child.Version,
        $"{scenario}: moved subtree retains operation audit and fresh versions");
      if (scenario == "cancelled") check(rows[b.Id].ParentId == null && rows[b.Id].Path == b.Path && rows[b.Id].Version == b.Version,
        "cancelled waiting move leaves its target unchanged while predecessor commits");
      observer.ChangeTracker.Clear();
      // Remove all fixture rows in one statement so self-referencing FKs see the final set.
      await observer.Subdivisions.Where(s => s.SubdivisionId >= 910001 && s.SubdivisionId <= 910006).ExecuteDeleteAsync(ct);

      ServiceProvider Provider(IInterceptor interceptor)
      {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(new Factory(new DbContextOptionsBuilder<ApplicationDbContext>()
          .UseNpgsql(observer.Database.GetConnectionString()).AddInterceptors(interceptor).Options));
        services.AddSingleton(permissions); services.AddSingleton(access);
        services.AddScoped<IUnitOfWorkFactory, UnitOfWorkFactory>();
        services.AddScoped(typeof(IReadRepository<>), typeof(ReadRepository<>));
        services.AddScoped<ISubdivisionService, SubdivisionService>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
      }
    }
  }

  private sealed class Factory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
  {
    public ApplicationDbContext CreateDbContext() => new(options);
  }
  private sealed class SaveGate : SaveChangesInterceptor
  {
    public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
    {
      Ready.TrySetResult(); await Release.Task.WaitAsync(ct); return result;
    }
  }
  private sealed class LockAttempt : DbCommandInterceptor
  {
    public TaskCompletionSource<int> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int TreeReads { get; private set; }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
      InterceptionResult<int> result, CancellationToken ct = default)
    {
      if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal))
        Ready.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
      return ValueTask.FromResult(result);
    }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
      InterceptionResult<DbDataReader> result, CancellationToken ct = default)
    {
      if (command.CommandText.Contains("FROM \"Subdivisions\"", StringComparison.Ordinal)) TreeReads++;
      return ValueTask.FromResult(result);
    }
  }
}
