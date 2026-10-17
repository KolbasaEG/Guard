using System.Data.Common;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;

internal static class ManagementModeConcurrencyTests
{
  // Runs only against the harness's isolated temporary PostgreSQL database.
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory,
    IPermissionService permissions, IDataAccessScopeService access, Action<bool, string> check)
  {
    using var http = new HttpClient(new RejectNetwork());
    var api = new DirectoryApiClient(http, Options.Create(new DirectoryApiOptions()), NullLogger<DirectoryApiClient>.Instance);
    var credentials = new ApiCredentialProtector(new EphemeralDataProtectionProvider());
    var normal = Service(factory, new Audit());
    foreach (var scenario in new[] { "mode wins", "mode rollback", "manual wins", "mode cancelled" }) {
      using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
      using var settingsCancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
      var ct = deadline.Token;
      var initial = (await normal.GetOverviewAsync(ct: ct)).Settings;
      await normal.SaveSettingsAsync(DataManagementMode.Local, false, new(2, 0), initial.Version, ct);
      await using var observer = await factory.CreateDbContextAsync(ct);
      var before = await observer.SynchronizationSettings.AsNoTracking().SingleAsync(ct);
      var modeFirst = scenario is "mode wins" or "mode rollback";
      var manualProbe = new LockProbe(modeFirst);
      var settingsProbe = new LockProbe(false);
      var save = new SavedGate { Fail = scenario == "mode rollback" };
      var manualFactory = new Factory(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(observer.Database.GetConnectionString())
        .AddInterceptors(modeFirst ? new IInterceptor[] { manualProbe } : [manualProbe, save]).Options);
      var settingsFactory = new Factory(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(observer.Database.GetConnectionString())
        .AddInterceptors(modeFirst ? new IInterceptor[] { settingsProbe, save } : [settingsProbe]).Options);
      var events = new Audit();
      var settingsService = Service(settingsFactory, events);
      var registrations = new ServiceCollection(); registrations.AddLogging();
      registrations.AddSingleton<IDbContextFactory<ApplicationDbContext>>(manualFactory);
      registrations.AddSingleton(permissions); registrations.AddSingleton(access);
      registrations.AddScoped<IUnitOfWorkFactory, UnitOfWorkFactory>();
      registrations.AddScoped(typeof(IReadRepository<>), typeof(ReadRepository<>));
      registrations.AddScoped<IPersonalEditorService, PersonalService>();
      await using var provider = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
      await using var scope = provider.CreateAsyncScope();
      var people = scope.ServiceProvider.GetRequiredService<IPersonalEditorService>();
      var marker = Guid.NewGuid().ToString("N");
      var person = new PersonalFieldsDto { LastName = "Mode race", FirstName = scenario, PersonalNumber = marker };
      Task<Guid> manual = people.CreateFromDtoAsync(new(person), ct);
      Task? changing = null;
      try {
        // The manual operation has already passed its preliminary Local check in both orders.
        if (modeFirst) await manualProbe.Ready.Task.WaitAsync(ct);
        else await save.Ready.Task.WaitAsync(ct);
        changing = settingsService.SaveSettingsAsync(DataManagementMode.Api, false, new(3, 0), before.Version, settingsCancellation.Token);
        if (modeFirst) {
          await save.Ready.Task.WaitAsync(ct);
          manualProbe.Release.TrySetResult();
          await WaitForLockAsync(await manualProbe.Ready.Task.WaitAsync(ct), manual);
        }
        else await WaitForLockAsync(await settingsProbe.Ready.Task.WaitAsync(ct), changing);
        var pending = await observer.SynchronizationSettings.AsNoTracking().SingleAsync(ct);
        check(pending.Mode == DataManagementMode.Local && pending.Version == before.Version &&
          !await observer.Personals.AnyAsync(p => p.PersonalNumber == marker, ct),
          $"{scenario}: independent observer sees no partial settings or employee before commit");
        check(events.Count == 0, $"{scenario}: settings audit is not emitted before commit");
        if (scenario == "mode cancelled") {
          settingsCancellation.Cancel();
          await PostgresTests.ThrowsAsync<OperationCanceledException>(() => changing, check,
            "mode switch can be cancelled while manual transaction still holds the lock");
        }
      }
      finally { manualProbe.Release.TrySetResult(); save.Release.TrySetResult(); }
      if (scenario == "mode wins") {
        await changing!;
        await PostgresTests.ThrowsAsync<EntityRuleException>(() => manual, check,
          "already-started manual command rechecks committed API mode and rejects its SQL write");
      }
      else {
        await manual;
        if (scenario == "mode rollback")
          await PostgresTests.ThrowsAsync<OperationCanceledException>(() => changing!, check,
            "mode switch rolls back after SQL save and releases waiting manual command");
        else if (scenario == "manual wins") await changing!;
      }
      var after = await observer.SynchronizationSettings.AsNoTracking().SingleAsync(ct);
      var persisted = await observer.Personals.AsNoTracking().SingleOrDefaultAsync(p => p.PersonalNumber == marker, ct);
      var switched = scenario is "mode wins" or "manual wins";
      check(after.Mode == (switched ? DataManagementMode.Api : DataManagementMode.Local) &&
        (switched ? after.Version != before.Version && after.ModifiedBy == "test-root" && after.ModifiedAt?.Kind == DateTimeKind.Utc :
          after.Version == before.Version && after.ModifiedBy == before.ModifiedBy && after.ModifiedAt == before.ModifiedAt),
        $"{scenario}: mode, version and audit describe only committed settings");
      check(scenario == "mode wins" ? persisted == null : persisted != null && persisted.CreatedBy == "test-root" && persisted.InsertedDate.Kind == DateTimeKind.Utc,
        $"{scenario}: employee and captured UTC actor agree with transaction ordering");
      if (scenario != "mode wins")
        check(persisted!.Id == await manual, $"{scenario}: DTO create returns the committed employee identifier");
      check(events.Count == (switched ? 1 : 0), $"{scenario}: only committed mode switch emits one settings event");
      await normal.SaveSettingsAsync(DataManagementMode.Local, false, new(2, 0), after.Version, ct);
      // A fresh application operation also proves lock/connection cleanup after rejection or cancellation.
      var later = new PersonalFieldsDto { LastName = "Mode race", FirstName = "After race" };
      var laterId = await people.CreateFromDtoAsync(new(later), ct);
      check(await observer.Personals.AnyAsync(p => p.Id == laterId, ct), $"{scenario}: same service can write again with a fresh operation context");
      await observer.Personals.Where(p => p.PersonalNumber == marker || p.Id == laterId).ExecuteDeleteAsync(ct);

      async Task WaitForLockAsync(int pid, Task operation)
      {
        bool waiting;
        do {
          waiting = await observer.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_locks WHERE pid = {pid} AND locktype = 'advisory' AND NOT granted").SingleAsync(ct) > 0;
          if (!waiting && !operation.IsCompleted) await Task.Delay(10, ct);
        } while (!waiting && !operation.IsCompleted);
        check(waiting, $"{scenario}: PostgreSQL confirms competing transaction is waiting on the management lock");
      }
    }

    SynchronizationService Service(IDbContextFactory<ApplicationDbContext> contexts, Audit audit) => new(contexts, permissions,
      new ApiSynchronizationRunner(contexts, api, permissions, audit, NullLogger<ApiSynchronizationRunner>.Instance, credentials),
      audit, credentials, NullLogger<SynchronizationService>.Instance);
  }

  private sealed class Factory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
  {
    public ApplicationDbContext CreateDbContext() => new(options);
  }
  private sealed class LockProbe(bool pause) : DbCommandInterceptor
  {
    public TaskCompletionSource<int> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
      InterceptionResult<int> result, CancellationToken ct = default)
    {
      if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal)) {
        Ready.TrySetResult(((NpgsqlConnection)command.Connection!).ProcessID);
        if (pause) await Release.Task.WaitAsync(ct);
      }
      return result;
    }
  }
  private sealed class SavedGate : SaveChangesInterceptor
  {
    public bool Fail { get; init; }
    public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
    {
      Ready.TrySetResult(); await Release.Task.WaitAsync(ct);
      if (Fail) throw new OperationCanceledException("Synthetic settings failure after SQL save before commit");
      return result;
    }
  }
  private sealed class Audit : IAuditService
  {
    public int Count { get; private set; }
    public void LogIdentityEvent(AuditEventType type, string? user, string? ipAddress = null, string? details = null)
    {
      if (type == AuditEventType.SynchronizationSettingsChanged) Count++;
    }
  }
  private sealed class RejectNetwork : HttpMessageHandler
  {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
      throw new InvalidOperationException("Mode concurrency tests must not send HTTP requests.");
  }
}
