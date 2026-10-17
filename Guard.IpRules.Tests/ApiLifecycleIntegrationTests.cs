using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

internal static class ApiLifecycleIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    // Предыдущие проверки могли перевести режим в локальный; возвращаем его через серверную операцию Root.
    var permissions = new PermissionStub();
    var client = new ClientStub();
    var credentials = new ApiCredentialProtector(new EphemeralDataProtectionProvider());
    var runner = new ApiSynchronizationRunner(factory, client, permissions, new AuditStub(), NullLogger<ApiSynchronizationRunner>.Instance, credentials);
    var service = new SynchronizationService(factory, permissions, runner, new AuditStub(), credentials, NullLogger<SynchronizationService>.Instance);
    var settings = (await service.GetOverviewAsync()).Settings;
    await service.SaveSettingsAsync(DataManagementMode.Local, false, new(2, 0), settings.Version,
      apiBaseUrl: "https://example.invalid/api", apiUsername: "test", apiPassword: "test");
    var markerTime = new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    Guid employeeId;
    await using (var db = await factory.CreateDbContextAsync())
    {
      var subdivision = await db.Subdivisions.FirstAsync(s => s.ExternalId != null);
      var employee = new Personal { ExternalId = 990001, ImportedFromApi = true, FirstName = "Lifecycle", LastName = "Employee",
        SubdivisionId = subdivision.Id, PersonalSubdivisionId = subdivision.ExternalId!.Value, SourceUpdatedAt = markerTime.AddDays(-1).UtcDateTime };
      db.Add(employee); await db.SaveChangesAsync(); employeeId = employee.Id;
    }
    settings = (await service.GetOverviewAsync()).Settings;
    await service.SaveSettingsAsync(DataManagementMode.Api, false, new(2, 0), settings.Version, completeSnapshotGuaranteed: true);
    client.Snapshot = new([], [], []) { ExitEvents = [new() { PersonalId = 990001, EventAt = markerTime }] };
    var exit = await service.RunAsync(updatedSince: new(2026, 10, 1));
    check(exit.Status == SynchronizationStatus.Succeeded && exit.Archived == 1 && exit.ExitEvents == 1, "explicit exit event archives imported employee atomically");
    await using (var db = await factory.CreateDbContextAsync())
      check((await db.Personals.FindAsync(employeeId))!.ArchivedBySynchronization && await db.Set<ApiPersonnelExit>().AnyAsync(e => e.PersonalExternalId == 990001), "exit archive origin and replay marker persisted together");
    var repeat = await service.RunAsync(updatedSince: new(2026, 10, 1));
    check(repeat.Status == SynchronizationStatus.Succeeded && repeat.Archived == 0 && repeat.RepeatedExitEvents == 1, "replayed exit event is idempotent");
    long subdivisionExternalId;
    await using (var db = await factory.CreateDbContextAsync()) subdivisionExternalId = (await db.Personals.FindAsync(employeeId))!.PersonalSubdivisionId;
    var beforeConflict = (await service.GetOverviewAsync()).Settings;
    var ambiguous = new ApiPersonal { Id = 990001, SubdivisionId = subdivisionExternalId, FirstName = "Ambiguous", LastName = "Employee", UpdatedAt = markerTime.ToOffset(TimeSpan.FromHours(3)).AddTicks(9) };
    client.Snapshot = new([], [], [ambiguous]);
    var storedConflict = await service.RunAsync(updatedSince: new(2026, 10, 1));
    check(storedConflict.Status == SynchronizationStatus.Failed && storedConflict.Error!.Contains("Неоднозначное"), "stored exit equality rejects delta at PostgreSQL precision across offsets");
    await using (var db = await factory.CreateDbContextAsync())
    {
      var employee = (await db.Personals.FindAsync(employeeId))!;
      var control = await db.SynchronizationSettings.SingleAsync();
      check(employee.Status == Status.Archived && employee.FirstName == "Lifecycle" && (await db.Set<ApiPersonnelExit>().FindAsync(990001L))!.EventAt == markerTime.UtcDateTime,
        "ambiguous stored exit leaves employee and marker unchanged");
      check(control.LastChangesSynchronizedAt == beforeConflict.LastChangesSynchronizedAt && control.ClassifiersSynchronizedAt == beforeConflict.ClassifiersSynchronizedAt && storedConflict.Added == 0 && storedConflict.Updated == 0,
        "ambiguous stored exit does not advance checkpoints or counters");
    }
    ambiguous.UpdatedAt = markerTime.AddSeconds(-1);
    var stale = await service.RunAsync(updatedSince: new(2026, 10, 1));
    check(stale.Status == SynchronizationStatus.Succeeded && stale.Restored == 0 && stale.Updated == 0, "older employee state remains skipped after stored exit");
    var returned = new ApiPersonal { Id = 990001, SubdivisionId = subdivisionExternalId, FirstName = "Returned", LastName = "Employee", UpdatedAt = markerTime.AddHours(1) };
    client.Snapshot = client.Snapshot with { Personnel = [returned], ExitEvents = [new() { PersonalId = 990001, EventAt = markerTime }] };
    var restore = await service.RunAsync(updatedSince: new(2026, 10, 1));
    check(restore.Status == SynchronizationStatus.Succeeded && restore.Restored == 1 && restore.Archived == 0, "newer in-scope state wins over replayed old exit event");
    client.Snapshot = client.Snapshot with { Personnel = [] };
    await service.RunAsync(updatedSince: new(2026, 10, 1));
    await using (var db = await factory.CreateDbContextAsync())
      check((await db.Personals.FindAsync(employeeId))!.Status == Status.Modified, "old event cannot rearchive returned employee when next delta is empty");
    client.Snapshot = new([], [], []) { ExitEvents = [new() { PersonalId = 99999999, EventAt = markerTime }] };
    var unknown = await service.RunAsync(updatedSince: new(2026, 10, 1));
    check(unknown.Status == SynchronizationStatus.Succeeded && unknown.UnknownExitEmployees == 1, "unknown employee event is recorded without creating incomplete employee");
    client.Snapshot = ApiSynchronizationTests.Snapshot() with { Personnel = [new() { Id = 99999999, SubdivisionId = 200, FirstName = "Unknown", LastName = "Employee", UpdatedAt = markerTime }] };
    var unknownConflict = await service.RunAsync();
    check(unknownConflict.Status == SynchronizationStatus.Failed && unknownConflict.Error!.Contains("Неоднозначное"), "full snapshot rejects equality with stored unknown employee exit");
    await using (var db = await factory.CreateDbContextAsync())
      check(!await db.Personals.AnyAsync(p => p.ExternalId == 99999999) && (await db.Set<ApiPersonnelExit>().FindAsync(99999999L))!.EventAt == markerTime.UtcDateTime,
        "ambiguous unknown employee creates no card and preserves marker");
    client.Snapshot = new([], [], [returned]) { ExitEvents = [new() { PersonalId = 990001, EventAt = returned.UpdatedAt }] };
    var conflict = await service.RunAsync(updatedSince: new(2026, 10, 1));
    check(conflict.Status == SynchronizationStatus.Failed && conflict.Archived == 0, "equal state and exit timestamps reject ambiguous transaction");
    await using (var db = await factory.CreateDbContextAsync())
      await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Personals\" ADD CONSTRAINT \"CK_TestLifecycleFailure\" CHECK (\"FirstName\" <> 'RejectLifecycle') NOT VALID");
    returned.FirstName = "RejectLifecycle"; returned.UpdatedAt = markerTime.AddHours(2);
    client.Snapshot = new([], [], [returned]) { ExitEvents = [new() { PersonalId = 990001, EventAt = markerTime.AddHours(3) }, new() { PersonalId = 990099, EventAt = markerTime.AddHours(3) }] };
    var rolledBack = await service.RunAsync(updatedSince: new(2026, 10, 1));
    await using (var db = await factory.CreateDbContextAsync())
    {
      check(rolledBack.Status == SynchronizationStatus.Failed && rolledBack.ExitEvents == 0 && (await db.Personals.FindAsync(employeeId))!.Status == Status.Modified,
        "database failure rolls back employee archive and clears application counters");
      check((await db.Set<ApiPersonnelExit>().FindAsync(990001L))!.EventAt == markerTime.UtcDateTime && !await db.Set<ApiPersonnelExit>().AnyAsync(e => e.PersonalExternalId == 990099),
        "failed save rolls back advanced and newly inserted exit markers");
      await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Personals\" DROP CONSTRAINT \"CK_TestLifecycleFailure\"");
    }
    settings = (await service.GetOverviewAsync()).Settings;
    await service.SaveSettingsAsync(DataManagementMode.Local, false, new(2, 0), settings.Version);
    await using (var db = await factory.CreateDbContextAsync())
    {
      (await db.Personals.FindAsync(employeeId))!.Status = Status.Blocked; await db.SaveChangesAsync();
    }
    settings = (await service.GetOverviewAsync()).Settings;
    await service.SaveSettingsAsync(DataManagementMode.Api, false, new(2, 0), settings.Version, completeSnapshotGuaranteed: true);
    client.Snapshot = new([], [], []) { ExitEvents = [new() { PersonalId = 990001, EventAt = markerTime.AddHours(4) }] };
    await service.RunAsync(updatedSince: new(2026, 10, 1));
    await using (var db = await factory.CreateDbContextAsync())
      check((await db.Personals.FindAsync(employeeId))!.Status == Status.ArchivedBlocked, "exit preserves employee block while archiving");
    returned.FirstName = "Returned"; returned.UpdatedAt = markerTime.AddHours(5);
    client.Snapshot = client.Snapshot with { Personnel = [returned] };
    await service.RunAsync(updatedSince: new(2026, 10, 1));
    await using (var db = await factory.CreateDbContextAsync())
      check((await db.Personals.FindAsync(employeeId))!.Status == Status.Blocked, "return restores blocked state without removing manual block");
    var beforeQueueFetches = client.FetchCalls;
    client.Snapshot = new([], [], []);
    using var pageLifetime = new CancellationTokenSource();
    var queued = await service.RunAsync(pageLifetime.Token, new(2026, 10, 1), enqueue: true);
    pageLifetime.Cancel();
    check(queued.Status == SynchronizationStatus.Queued && queued.QueuedAt.HasValue && (await ReadRunAsync(factory, queued.Id)).RequestedSettingsVersion.HasValue, "manual request stored durably before page cancellation");
    await PostgresTests.ThrowsAsync<EntityRuleException>(() => service.RunAsync(enqueue: true), check, "second queued request rejected");
    await runner.RecoverInterruptedAsync(CancellationToken.None);
    await using (var db = await factory.CreateDbContextAsync())
      check((await db.SynchronizationRuns.FindAsync(queued.Id))!.Status == SynchronizationStatus.Queued, "restart recovery preserves pending queued request");
    var background = await runner.RunAsync((await ReadRunAsync(factory, queued.Id)).ActorId, queued.PoolId, false, CancellationToken.None, queued.UpdatedSince, queued.Id);
    check(background.Id == queued.Id && background.Status == SynchronizationStatus.Succeeded, "background execution completes same queued record independently of page token");
    var outdated = await service.RunAsync(enqueue: true);
    settings = (await service.GetOverviewAsync()).Settings;
    await service.SaveSettingsAsync(DataManagementMode.Api, false, new(3, 0), settings.Version);
    var rejected = await runner.RunAsync((await ReadRunAsync(factory, outdated.Id)).ActorId, outdated.PoolId, false, CancellationToken.None, outdated.UpdatedSince, outdated.Id);
    check(rejected.Status == SynchronizationStatus.Failed && rejected.Error!.Contains("Настройки изменились"), "queued run does not silently use changed source configuration");
    var denied = await service.RunAsync(enqueue: true);
    permissions.Root = false;
    var revoked = await runner.RunAsync((await ReadRunAsync(factory, denied.Id)).ActorId, denied.PoolId, false, CancellationToken.None, denied.UpdatedSince, denied.Id);
    check(revoked.Status == SynchronizationStatus.Failed && client.FetchCalls == beforeQueueFetches + 1, "queued run rechecks revoked permissions before contacting API");
    permissions.Root = true;
    var hostedRequest = await service.RunAsync(updatedSince: new(2026, 10, 1), enqueue: true);
    await using (var provider = new ServiceCollection().AddSingleton(factory).AddScoped(_ => runner).BuildServiceProvider())
    using (var worker = new ApiSynchronizationWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ApiSynchronizationWorker>.Instance))
    {
      await worker.StartAsync(CancellationToken.None);
      using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      SynchronizationStatus status;
      do
      {
        await using var db = await factory.CreateDbContextAsync(deadline.Token);
        status = (await db.SynchronizationRuns.AsNoTracking().SingleAsync(r => r.Id == hostedRequest.Id, deadline.Token)).Status;
        if (status == SynchronizationStatus.Queued || status == SynchronizationStatus.Running) await Task.Delay(50, deadline.Token);
      } while (status == SynchronizationStatus.Queued || status == SynchronizationStatus.Running);
      check(status == SynchronizationStatus.Succeeded, "hosted worker consumes durable queue with operation-scoped services");
      await worker.StopAsync(deadline.Token);
    }
    await service.CheckConnectionAsync();
    check(client.Pings == 1, "Root connection check uses ping without importing data");
    await using (var db = await factory.CreateDbContextAsync())
    {
      var control = await db.SynchronizationSettings.SingleAsync();
      control.LastFullSynchronizedAt = null; control.LastChangesSynchronizedAt = DateTime.UtcNow.AddDays(-4);
      control.LastScheduledDate = null; control.ScheduleEnabled = true; control.DailyTime = TimeOnly.MinValue;
      await db.SaveChangesAsync();
    }
    var resumed = await runner.RunAsync("system:api-sync", null, true, CancellationToken.None);
    var recovery = (await service.GetOverviewAsync()).Settings.RecoveryRequiredFrom;
    check(resumed.Status == SynchronizationStatus.Succeeded && recovery.HasValue, "successful daily window after downtime retains missing-period warning");
    await service.RunAsync(updatedSince: DateOnly.FromDateTime(DateTime.Now).AddDays(-1));
    check((await service.GetOverviewAsync()).Settings.RecoveryRequiredFrom == recovery, "another successful narrow window does not erase recovery requirement");
    await service.RunAsync(updatedSince: recovery);
    check((await service.GetOverviewAsync()).Settings.RecoveryRequiredFrom == null, "successful explicit replay covering missing period clears recovery requirement");
    var day = new DateTime(2026, 10, 20);
    var clock = new TestClock { Now = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(day.AddHours(23).AddMinutes(58), TimeZoneInfo.Local)) };
    var clockRunner = new ApiSynchronizationRunner(factory, client, permissions, new AuditStub(), NullLogger<ApiSynchronizationRunner>.Instance, credentials, timeProvider: clock);
    await using (var db = await factory.CreateDbContextAsync())
    {
      var control = await db.SynchronizationSettings.SingleAsync();
      control.LastScheduledDate = null; control.DailyTime = new(23, 58); await db.SaveChangesAsync();
    }
    client.OnFetch = () => clock.Now = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(day.AddDays(1).AddMinutes(2), TimeZoneInfo.Local));
    var midnight = await clockRunner.RunAsync("system:api-sync", null, true, CancellationToken.None);
    var midnightSettings = await ReadSettingsAsync(factory);
    check(midnight.Status == SynchronizationStatus.Succeeded && TimeZoneInfo.ConvertTimeFromUtc(midnight.StartedAt, TimeZoneInfo.Local).Date != TimeZoneInfo.ConvertTimeFromUtc(midnight.CompletedAt!.Value, TimeZoneInfo.Local).Date && midnightSettings.LastScheduledDate == DateOnly.FromDateTime(day),
      "cross-midnight success records scheduled start day rather than completion day");
    check(midnight.UpdatedSince == DateOnly.FromDateTime(day).AddDays(-1) && client.LastUpdatedSince == midnight.UpdatedSince,
      "cross-midnight export uses start day minus one in HTTP request");
    check(!SynchronizationSchedule.IsDue(midnightSettings, new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(day.AddHours(23).AddMinutes(59), TimeZoneInfo.Local))) &&
      SynchronizationSchedule.IsDue(midnightSettings, new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(day.AddDays(1).AddHours(23).AddMinutes(58), TimeZoneInfo.Local))),
      "same scheduled day is not repeated and next day remains due");
    clock.Now = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(day.AddDays(1).AddHours(23).AddMinutes(58), TimeZoneInfo.Local));
    client.OnFetch = () => throw new OperationCanceledException();
    var scheduledFailure = await clockRunner.RunAsync("system:api-sync", null, true, CancellationToken.None);
    midnightSettings = await ReadSettingsAsync(factory);
    check(scheduledFailure.Status == SynchronizationStatus.Failed && midnightSettings.LastScheduledDate == DateOnly.FromDateTime(day) && SynchronizationSchedule.IsDue(midnightSettings, clock.Now),
      "failed scheduled export preserves completed day and permits retry");
    using var cancelled = new CancellationTokenSource();
    client.OnFetch = () => { cancelled.Cancel(); throw new OperationCanceledException(cancelled.Token); };
    var scheduledCancellation = await clockRunner.RunAsync("system:api-sync", null, true, cancelled.Token);
    check(scheduledCancellation.Status == SynchronizationStatus.Cancelled && (await service.GetOverviewAsync()).Settings.LastScheduledDate == DateOnly.FromDateTime(day),
      "cancelled scheduled export does not advance completed day");
    client.OnFetch = null;
    await using (var db = await factory.CreateDbContextAsync())
    {
      var control = await db.SynchronizationSettings.SingleAsync(); control.ScheduleEnabled = false; await db.SaveChangesAsync();
    }
    permissions.Root = false;
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.CheckConnectionAsync(), check, "ping operation restricted to Root");
  }
  private static async Task<SynchronizationSettings> ReadSettingsAsync(IDbContextFactory<ApplicationDbContext> factory)
  {
    await using var db = await factory.CreateDbContextAsync();
    return await db.SynchronizationSettings.AsNoTracking().SingleAsync();
  }
  private static async Task<SynchronizationRun> ReadRunAsync(IDbContextFactory<ApplicationDbContext> factory, Guid id)
  {
    await using var db = await factory.CreateDbContextAsync();
    return await db.SynchronizationRuns.AsNoTracking().SingleAsync(r => r.Id == id);
  }
  private sealed class ClientStub : IDirectoryApiClient
  {
    public ApiSnapshot Snapshot { get; set; } = new([], [], []);
    public int FetchCalls { get; private set; }
    public int Pings { get; private set; }
    public Action? OnFetch { get; set; }
    public DateOnly? LastUpdatedSince { get; private set; }
    public Task<ApiSnapshot> FetchAsync(CancellationToken ct) { FetchCalls++; OnFetch?.Invoke(); return Task.FromResult(Snapshot); }
    public Task<ApiSnapshot> FetchAsync(string baseUrl, int pageSize, CancellationToken ct, string? username = null, string? password = null, DateOnly? updatedSince = null, long? headSubdivisionId = null, Func<string, CancellationToken, Task>? phaseChanged = null)
    { LastUpdatedSince = updatedSince; return FetchAsync(ct); }
    public Task<ApiSnapshot> FetchDiagnosticAsync(string baseUrl, int pageSize, string username, string password, DateOnly updatedSince, CancellationToken ct, IProgress<string>? progress = null) => FetchAsync(ct);
    public Task<ApiSnapshot> ResolveDependenciesAsync(ApiSnapshot snapshot, IReadOnlySet<(int Type, int Code)> knownClassifiers, IReadOnlySet<long> knownSubdivisions, string baseUrl, string username, string password, CancellationToken ct) => Task.FromResult(snapshot);
    public Task PingAsync(string baseUrl, string username, string password, CancellationToken ct) { Pings++; return Task.CompletedTask; }
  }
  private sealed class TestClock : TimeProvider
  {
    public DateTimeOffset Now { get; set; }
    public override DateTimeOffset GetUtcNow() => Now;
  }
  private sealed class PermissionStub : IPermissionService
  {
    public bool Root { get; set; } = true;
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(new UserAccessSnapshot("test-api-root", Root, []));
    public Task<UserAccessSnapshot> GetForUserAsync(string userId, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string permission, CancellationToken ct = default) => Task.FromResult(Root);
    public Task RequireAsync(string permission, CancellationToken ct = default) => Root ? Task.CompletedTask : throw new UnauthorizedAccessException();
  }
  private sealed class AuditStub : IAuditService
  {
    public void LogIdentityEvent(AuditEventType eventType, string? userName, string? ipAddress = null, string? details = null) { }
  }
}
