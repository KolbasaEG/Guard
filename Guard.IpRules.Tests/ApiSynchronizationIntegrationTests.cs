using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

internal static class ApiSynchronizationIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    var permissions = new PermissionsStub();
    var api = new ClientStub();
    var audit = new AuditStub();
    var credentials = new ApiCredentialProtector(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
    var runner = new ApiSynchronizationRunner(factory, api, permissions, audit, NullLogger<ApiSynchronizationRunner>.Instance, credentials);
    var service = new SynchronizationService(factory, permissions, runner, audit, credentials);
    await using (var db = await factory.CreateDbContextAsync())
    {
      db.AddRange(new Classifier { Id = 9000000, Type = 999, Code = 1, ClassifierName = "Local", Value = "Local" },
        new Subdivision { SubdivisionId = 9000000, Name = "Local", Path = "/9000000/" },
        new Personal { PersonalId = 9000000, FirstName = "Local", LastName = "Local" });
      await db.SaveChangesAsync();
      var settings = await db.SynchronizationSettings.SingleAsync();
      await service.SaveSettingsAsync(DataManagementMode.Api, false, new(2, 0), settings.Version,
        apiBaseUrl: "https://example.invalid/api", apiPageSize: 25, apiUsername: "test", apiPassword: "test-password");
      var saved = await service.GetOverviewAsync();
      check(saved.Settings.ApiBaseUrl == "https://example.invalid/api" && saved.Settings.ApiPageSize == 25,
        "Root source and page size settings persist in database");
      check(saved.Settings.ApiUsername == "test" && saved.Settings.HasApiPassword && saved.Settings.ProtectedApiPassword == null,
        "overview returns username and password presence without protected or plaintext password");
      await using (var credentialDb = await factory.CreateDbContextAsync())
      {
        var stored = await credentialDb.SynchronizationSettings.SingleAsync();
        var encrypted = stored.ProtectedApiPassword;
        check(encrypted != null && encrypted != "test-password" && credentials.Unprotect(encrypted) == "test-password", "API password is encrypted at rest");
        await service.SaveSettingsAsync(DataManagementMode.Api, false, new(2, 0), saved.Settings.Version, apiUsername: "renamed", apiPassword: "");
        stored = await credentialDb.SynchronizationSettings.AsNoTracking().SingleAsync();
        check(stored.ApiUsername == "renamed" && stored.ProtectedApiPassword == encrypted, "empty password preserves current secret while changing login");
        await service.SaveSettingsAsync(DataManagementMode.Api, false, new(2, 0), stored.Version, apiUsername: "test", apiPassword: "replacement-password");
        stored = await credentialDb.SynchronizationSettings.AsNoTracking().SingleAsync();
        check(credentials.Unprotect(stored.ProtectedApiPassword!) == "replacement-password", "Root can replace API password");
        saved = await service.GetOverviewAsync();
      }
      foreach (var invalid in new[] { ("http://example.invalid/api", 25), ("https://example.invalid/api", 0), ("https://example.invalid/api", 501), ("https://user:secret@example.invalid/api", 25) })
      {
        try { await service.SaveSettingsAsync(DataManagementMode.Api, false, new(2, 0), saved.Settings.Version, apiBaseUrl: invalid.Item1, apiPageSize: invalid.Item2); check(false, "invalid source settings rejected"); }
        catch (ArgumentException) { check(true, "invalid source settings rejected"); }
      }
    }
    var first = await runner.RunAsync("test-api-root", null, false, CancellationToken.None, new DateOnly(2000, 8, 10));
    check(first.Status == SynchronizationStatus.Succeeded && first.UpdatedSince == new DateOnly(2000, 8, 10), "initial synchronization with date succeeds without prior full import");
    var listService = new PersonalService(null!, null!, null!, NullLogger<PersonalService>.Instance, permissions,
      new DataAccessScopeService(permissions, factory), factory);
    check((await listService.SearchAsync(new(OwnSubdivision: true))).Count == 0, "Root own-personnel mode stays empty without own employee binding");
    var rootList = await listService.SearchAsync(new(AllSubdivisions: true, Take: 100));
    await using (var db = await factory.CreateDbContextAsync())
      check(rootList.Count == await db.Personals.CountAsync(p => p.Status == Status.Inserted || p.Status == Status.Modified || p.Status == Status.Blocked),
        "Root all-personnel mode returns every active employee without own subdivision");
    check((await service.GetOverviewAsync(allSubdivisions: true)).ActualAt.HasValue,
      "Root global freshness reports successful system run independently of local unsynchronized records");
    Guid employeeId, departmentId, version;
    await using (var db = await factory.CreateDbContextAsync())
    {
      var department = await db.Subdivisions.SingleAsync(s => s.ExternalId == 200);
      var root = await db.Subdivisions.SingleAsync(s => s.ExternalId == 100);
      var employee = await db.Personals.SingleAsync(p => p.ExternalId == 10);
      employeeId = employee.Id; departmentId = department.Id; version = employee.Version;
      check(department.ParentId == root.Id && department.Path == root.Path + department.SubdivisionId + "/", "import builds consistent local parent and materialized path");
      check(department.SubdivisionId > 9000000 && employee.PersonalId > 9000000,
        "import advances local sequences beyond explicitly seeded identifiers");
      check(await db.Personals.CountAsync(p => p.PersonalSubdivisionId == 200) == 2, "import supports several employees in one external subdivision");
      check(employee.CreatedBy == "test-api-root" && employee.SourceUpdatedAt!.Value.Hour == 12, "import retains initiator audit and UTC source timestamp");
      check(department.OrganTypeId == null && root.OrganTypeId == 363, "organ type classifier reference applied");
      employee.FirstName = "Forbidden";
      await PostgresTests.ThrowsAsync<EntityRuleException>(() => db.SaveChangesAsync(), check, "API mode blocks direct manual writes before persistence");
    }
    var repeat = await runner.RunAsync("test-api-root", null, false, CancellationToken.None);
    check(repeat.Status == SynchronizationStatus.Succeeded && repeat.Added == 0 && repeat.Updated == 0 && repeat.Archived == 0, "repeated unchanged snapshot is idempotent");
    await using (var db = await factory.CreateDbContextAsync())
      check((await db.Personals.SingleAsync(p => p.Id == employeeId)).Version == version, "unchanged employee version remains stable");
    var fullSnapshot = api.Snapshot;
    var changedPerson = System.Text.Json.JsonSerializer.Deserialize<ApiPersonal>(System.Text.Json.JsonSerializer.Serialize(fullSnapshot.Personnel[0]))!;
    changedPerson.FirstName = "Delta";
    api.Snapshot = new ApiSnapshot([], [], [changedPerson]);
    var delta = await service.RunAsync(updatedSince: new DateOnly(2000, 8, 10));
    check(delta.Status == SynchronizationStatus.Succeeded && delta.UpdatedSince == new DateOnly(2000, 8, 10), "Root manual delta resolves references against stored classifiers and subdivisions");
    await using (var db = await factory.CreateDbContextAsync())
    {
      check((await db.Personals.FindAsync(employeeId))!.FirstName == "Delta", "partial snapshot updates returned employee");
      check(delta.Archived == 0 && delta.Deactivated == 0 && await db.Classifiers.AnyAsync(c => c.ImportedFromApi && c.IsActive) &&
        !await db.Subdivisions.AnyAsync(s => s.ArchivedBySynchronization), "delta does not archive absent records or deactivate absent classifiers");
    }
    api.Snapshot = new ApiSnapshot([], [], []);
    var emptyDelta = await service.RunAsync(updatedSince: new DateOnly(2000, 8, 10));
    check(emptyDelta.Status == SynchronizationStatus.Succeeded && emptyDelta.Archived == 0 && emptyDelta.Deactivated == 0, "empty delta preserves all previously imported records");
    changedPerson.SubdivisionId = 999999;
    api.Snapshot = new ApiSnapshot([], [], [changedPerson]);
    var invalidDelta = await service.RunAsync(updatedSince: new DateOnly(2000, 8, 10));
    check(invalidDelta.Status == SynchronizationStatus.Failed && invalidDelta.Updated == 0, "delta with unknown stored subdivision is rejected atomically");
    api.Snapshot = new ApiSnapshot([], [new ApiSubdivision { Id = 100, ParentId = 200, Name = "Cycle", UpdatedAt = DateTimeOffset.UtcNow }], []);
    var cyclicDelta = await service.RunAsync(updatedSince: new DateOnly(2000, 8, 10));
    check(cyclicDelta.Status == SynchronizationStatus.Failed, "delta detects cycle spanning returned and stored subdivisions");
    api.Snapshot = new ApiSnapshot([], [], []);
    var scheduledSettings = (await service.GetOverviewAsync()).Settings;
    await service.SaveSettingsAsync(DataManagementMode.Api, true, TimeOnly.MinValue, scheduledSettings.Version);
    var scheduledDelta = await runner.RunAsync("system:api-sync", null, true, CancellationToken.None);
    check(scheduledDelta.Status == SynchronizationStatus.Succeeded && scheduledDelta.UpdatedSince ==
      DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(scheduledDelta.StartedAt, TimeZoneInfo.Local)).AddDays(-1), "scheduled request uses server calendar date of start minus one day");
    scheduledSettings = (await service.GetOverviewAsync()).Settings;
    await service.SaveSettingsAsync(DataManagementMode.Api, false, new(2, 0), scheduledSettings.Version);
    api.Snapshot = fullSnapshot;
    var assignments = new PersonalIpService(factory, new IpManagementAccessService(permissions, new DataAccessScopeService(permissions, factory)), NullLogger<PersonalIpService>.Instance);
    await PostgresTests.ThrowsAsync<EntityRuleException>(() => assignments.UpdateAsync(employeeId, []), check,
      "API mode rejects employee IP assignment even for Root");
    await using (var db = await factory.CreateDbContextAsync())
    {
      db.Users.Add(new ApplicationUser { Id = "forbidden-link", UserName = "forbidden-link", PersonalId = employeeId });
      await PostgresTests.ThrowsAsync<EntityRuleException>(() => db.SaveChangesAsync(), check,
        "API mode rejects new manual account binding to employee");
    }

    // Перевод и очистка поля полного состояния.
    api.Snapshot.Personnel[0].SubdivisionId = 300;
    api.Snapshot.Personnel[0].PositionType = null; api.Snapshot.Personnel[0].PositionCode = null;
    var transfer = await runner.RunAsync("test-api-root", null, false, CancellationToken.None);
    check(transfer.Status == SynchronizationStatus.Succeeded, "employee transfer synchronization succeeds");
    await using (var db = await factory.CreateDbContextAsync())
    {
      var employee = await db.Personals.SingleAsync(p => p.Id == employeeId);
      var target = await db.Subdivisions.SingleAsync(s => s.ExternalId == 300);
      check(employee.SubdivisionId == target.Id && employee.PositionCode == null, "transfer preserves local identity and clears removed optional values");
    }
    var complete = api.Snapshot;
    api.Snapshot = complete with { Personnel = complete.Personnel.Where(p => p.Id != 10).ToList() };
    var disappearance = await runner.RunAsync("test-api-root", null, false, CancellationToken.None);
    check(disappearance.Status == SynchronizationStatus.Succeeded && disappearance.Archived == 1, "missing employee is archived after complete snapshot");
    await using (var db = await factory.CreateDbContextAsync())
      check((await db.Personals.SingleAsync(p => p.Id == employeeId)).ArchivedBySynchronization, "archive records synchronization origin");
    api.Snapshot = complete;
    var returned = await runner.RunAsync("test-api-root", null, false, CancellationToken.None);
    check(returned.Status == SynchronizationStatus.Succeeded && returned.Restored == 1, "returning employee is restored automatically");

    var settingsBeforeFailure = (await service.GetOverviewAsync(classifiers: true)).Settings.ClassifiersSynchronizedAt;
    api.ThrowOnFetch = true;
    var failure = await runner.RunAsync("test-api-root", null, false, CancellationToken.None);
    check(failure.Status == SynchronizationStatus.Failed && failure.Added == 0 && failure.Archived == 0, "fetch failure produces failed journal record with no applied changes");
    check((await service.GetOverviewAsync(classifiers: true)).Settings.ClassifiersSynchronizedAt == settingsBeforeFailure,
      "failed attempt does not advance actual data timestamp");
    api.ThrowOnFetch = false;

    // Ошибка после начала применения должна откатить изменения всех ресурсов.
    api.Snapshot = complete with { Personnel = complete.Personnel.Concat([new ApiPersonal {
      Id = 999, SubdivisionId = 200, LastName = "Rollback", FirstName = "Test", UpdatedAt = DateTimeOffset.UtcNow
    }]).ToList() };
    await using (var db = await factory.CreateDbContextAsync())
      await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Personals\" ADD CONSTRAINT \"test_sync_rollback\" CHECK (\"ExternalId\" IS DISTINCT FROM 999)");
    var rollback = await runner.RunAsync("test-api-root", null, false, CancellationToken.None);
    check(rollback.Status == SynchronizationStatus.Failed, "database error during apply fails entire synchronization");
    await using (var db = await factory.CreateDbContextAsync())
    {
      check(!await db.Personals.AnyAsync(p => p.ExternalId == 999), "failed apply leaves no partial employee insert");
      await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Personals\" DROP CONSTRAINT \"test_sync_rollback\"");
    }
    api.Snapshot = complete;

    // Два соединения не могут одновременно владеть глобальной блокировкой запуска.
    await using (var lockDb = await factory.CreateDbContextAsync())
    {
      await lockDb.Database.OpenConnectionAsync();
      await lockDb.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock(73460220)");
      try { await PostgresTests.ThrowsAsync<EntityRuleException>(() => runner.RunAsync("test-api-root", null, false, CancellationToken.None), check, "parallel synchronization is rejected across database connections"); }
      finally { await lockDb.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(73460220)"); }
    }
    var overview = await service.GetOverviewAsync(classifiers: true);
    await service.SaveSettingsAsync(DataManagementMode.Local, true, new(2, 0), overview.Settings.Version);
    await PostgresTests.ThrowsAsync<EntityRuleException>(() => runner.RunAsync("test-api-root", null, false, CancellationToken.None), check, "local mode rejects manual synchronization");
    await using (var db = await factory.CreateDbContextAsync())
    {
      var employee = await db.Personals.SingleAsync(p => p.Id == employeeId);
      employee.FirstName = "Local edit"; await db.SaveChangesAsync();
      check(employee.FirstName == "Local edit", "local mode restores manual editing");
      var operatorPersonal = await db.Personals.SingleAsync(p => p.ExternalId == 20);
      db.Users.Add(new ApplicationUser { Id = "pool-operator", UserName = "pool-operator", SecurityStamp = "test", PersonalId = operatorPersonal.Id });
      await db.SaveChangesAsync();
    }
    overview = await service.GetOverviewAsync(classifiers: true);
    await service.SaveSettingsAsync(DataManagementMode.Api, false, new(2, 0), overview.Settings.Version);
    permissions.Current = new("pool-operator", false, [Guard.Core.Identity.Permissions.Subdivisions.Read, Guard.Core.Identity.Permissions.Synchronization.Run]);
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetOverviewAsync(allSubdivisions: true), check,
      "non-Root cannot request global subdivision freshness");
    permissions.Current = new("pool-operator", false, [Guard.Core.Identity.Permissions.Subdivisions.Read, Guard.Core.Identity.Permissions.Synchronization.Run, Guard.Core.Identity.Permissions.Personals.Read]);
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => listService.SearchAsync(new(AllSubdivisions: true)), check,
      "non-Root cannot request all-personnel mode through service");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.RunAsync(updatedSince: new DateOnly(2000, 8, 10)), check, "manual date filter is restricted to Root on server");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetOverviewAsync(), check, "legacy sync permission cannot read Root overview");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetHistoryAsync(), check, "legacy sync permission cannot read history");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.RunAsync(), check, "legacy sync permission cannot start direct synchronization");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.RunAsync(enqueue: true), check, "legacy sync permission cannot enqueue synchronization");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveSettingsAsync(DataManagementMode.Api, false, new(2, 0), Guid.NewGuid(), apiUsername: "forbidden", apiPassword: "forbidden"), check, "operator cannot change Basic credentials");
    var poolActuality = await service.GetActualityAsync(Guard.Core.Identity.Permissions.Personals.Read);
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetActualityAsync(Guard.Core.Identity.Permissions.Classifiers.Read), check, "freshness requires permission to read requested directory");
    check(!Guard.Core.Identity.PermissionCatalog.All.Any(p => p.Code == Guard.Core.Identity.Permissions.Synchronization.Run), "legacy synchronization permission is absent from role catalog");
    await using (var db = await factory.CreateDbContextAsync())
    {
      var expected = await db.SynchronizationRuns.Where(r => r.PoolId == null && r.Status == SynchronizationStatus.Succeeded).MaxAsync(r => r.CompletedAt);
      check(poolActuality.ActualAt == expected, "ordinary user reads global successful date without access to credentials or history");
      var future = DateTime.UtcNow.AddDays(1);
      var poolHistory = new SynchronizationRun { ActorId = "pool-operator", PoolId = departmentId, Status = SynchronizationStatus.Succeeded, StartedAt = future, CompletedAt = future };
      var failedHistory = new SynchronizationRun { ActorId = "test-api-root", Status = SynchronizationStatus.Failed, StartedAt = future, CompletedAt = future };
      db.AddRange(poolHistory, failedHistory); await db.SaveChangesAsync();
      check((await service.GetActualityAsync(Guard.Core.Identity.Permissions.Personals.Read)).ActualAt == expected, "historical pool success and newer global failure do not advance system freshness");
      db.RemoveRange(poolHistory, failedHistory); await db.SaveChangesAsync();
      var oldRequest = new SynchronizationRun { ActorId = "pool-operator", PoolId = departmentId, Status = SynchronizationStatus.Queued, StartedAt = DateTime.UtcNow, QueuedAt = DateTime.UtcNow };
      db.Add(oldRequest); await db.SaveChangesAsync();
      var rejected = await runner.RunAsync(oldRequest.ActorId, oldRequest.PoolId, false, CancellationToken.None, queuedRunId: oldRequest.Id);
      check(rejected.Status == SynchronizationStatus.Failed, "legacy queued operator request is refused by background execution");
    }
    permissions.Current = new("test-api-root", true, []);
    api.Snapshot.Personnel[0].FirstName = "Сотрудник";
    api.Snapshot.Personnel[1].SubdivisionId = 200;
    api.Snapshot.Subdivisions[1].ParentId = 300;
    api.Snapshot = api.Snapshot with { Subdivisions = api.Snapshot.Subdivisions.Concat([new ApiSubdivision {
      Id = 400, ParentId = 200, Name = "Вложенный отдел", UpdatedAt = DateTimeOffset.UtcNow
    }]).ToList() };
    var moved = await runner.RunAsync("test-api-root", null, false, CancellationToken.None);
    check(moved.Status == SynchronizationStatus.Succeeded, "API subtree move succeeds");
    await using (var db = await factory.CreateDbContextAsync())
    {
      var branch = await db.Subdivisions.SingleAsync(s => s.ExternalId == 200);
      var nested = await db.Subdivisions.SingleAsync(s => s.ExternalId == 400);
      var parent = await db.Subdivisions.SingleAsync(s => s.ExternalId == 300);
      check(branch.Path == parent.Path + branch.SubdivisionId + "/" && nested.Path == branch.Path + nested.SubdivisionId + "/",
        "API branch move atomically updates descendant paths");
    }
    var beforeDisappear = api.Snapshot;
    api.Snapshot = beforeDisappear with {
      Subdivisions = beforeDisappear.Subdivisions.Where(s => s.Id is not (200 or 400)).ToList(),
      Personnel = beforeDisappear.Personnel.Where(p => p.Id != 20).ToList()
    };
    var removedBranch = await runner.RunAsync("test-api-root", null, false, CancellationToken.None);
    check(removedBranch.Status == SynchronizationStatus.Succeeded && removedBranch.Archived == 3,
      "missing branch, descendant and employee are archived after full snapshot");
    api.Snapshot = beforeDisappear;
    var returnedBranch = await runner.RunAsync("test-api-root", null, false, CancellationToken.None);
    check(returnedBranch.Status == SynchronizationStatus.Succeeded && returnedBranch.Restored == 3, "returning branch and employee are restored");
    api.Snapshot = beforeDisappear with { Classifiers = beforeDisappear.Classifiers.Where(c => c.Code != 10).ToList() };
    var inactive = await runner.RunAsync("test-api-root", null, false, CancellationToken.None);
    check(inactive.Status == SynchronizationStatus.Succeeded && inactive.Deactivated == 1, "missing imported classifier is deactivated");
    await using (var db = await factory.CreateDbContextAsync())
      check(!await db.Classifiers.Where(c => c.Type == 6 && c.Code == 10).Select(c => c.IsActive).SingleAsync(), "deactivated classifier retained for existing references");
    api.Snapshot = beforeDisappear;
    await runner.RunAsync("test-api-root", null, false, CancellationToken.None);
    var operatorRecord = beforeDisappear.Personnel.Single(p => p.Id == 20);
    api.Snapshot = beforeDisappear with { Personnel = beforeDisappear.Personnel.Where(p => p.Id != 20).ToList() };
    await runner.RunAsync("test-api-root", null, false, CancellationToken.None);
    overview = await service.GetOverviewAsync(classifiers: true);
    await service.SaveSettingsAsync(DataManagementMode.Local, false, new(2, 0), overview.Settings.Version);
    Guid operatorId;
    await using (var db = await factory.CreateDbContextAsync()) operatorId = (await db.Personals.SingleAsync(p => p.ExternalId == operatorRecord.Id)).Id;
    await using (var provider = new ServiceCollection().AddLogging().BuildServiceProvider())
    {
      var writes = new UnitOfWorkFactory(factory, permissions, provider, NullLogger<UnitOfWork>.Instance);
      var people = new PersonalService(null!, null!, writes, NullLogger<PersonalService>.Instance, permissions,
        new DataAccessScopeService(permissions, factory), factory);
      await people.SoftDeleteAsync(operatorId);
    }
    await using (var db = await factory.CreateDbContextAsync())
      check(!(await db.Personals.FindAsync(operatorId))!.ArchivedBySynchronization, "manual lifecycle decision clears previous automatic archive origin");
    overview = await service.GetOverviewAsync(classifiers: true);
    await service.SaveSettingsAsync(DataManagementMode.Api, false, new(2, 0), overview.Settings.Version);
    api.Snapshot = beforeDisappear;
    await runner.RunAsync("test-api-root", null, false, CancellationToken.None);
    await using (var db = await factory.CreateDbContextAsync())
      check((await db.Personals.FindAsync(operatorId))!.Status == Status.Deleted, "returning source record cannot undo manual deletion after automatic archive");
    overview = await service.GetOverviewAsync(classifiers: true);
    api.Pause = new(TaskCreationOptions.RunContinuationsAsynchronously);
    api.Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    var pendingRun = runner.RunAsync("test-api-root", null, false, CancellationToken.None);
    await api.Started.Task;
    await service.SaveSettingsAsync(DataManagementMode.Local, false, new(2, 0), overview.Settings.Version);
    var cancelled = await pendingRun;
    check(cancelled.Status == SynchronizationStatus.Cancelled, "switch to local mode cancels active fetch before applying data");
    api.Pause = null;
    overview = await service.GetOverviewAsync(classifiers: true);
    api.Snapshot = ApiSynchronizationTests.Snapshot();
    api.Snapshot.Subdivisions[0].ParentId = 2;
    await service.SaveSettingsAsync(DataManagementMode.Api, false, new(2, 0), overview.Settings.Version, apiHeadSubdivisionId: 100);
    overview = await service.GetOverviewAsync(classifiers: true);
    check(overview.Settings.ApiHeadSubdivisionId == 100, "Root head setting persists and reloads");
    var bounded = await service.RunAsync(updatedSince: new DateOnly(1962, 10, 1));
    check(bounded.Status == SynchronizationStatus.Succeeded, "configured head with unavailable external ancestor imports successfully");
    await using (var db = await factory.CreateDbContextAsync())
    {
      var head = await db.Subdivisions.SingleAsync(s => s.ExternalId == 100);
      check(head.ParentId == null && head.ParentSubdivisionId == null && head.ExternalParentId == 2 && head.Path == $"/{head.SubdivisionId}/",
        "import preserves external parent and builds local head path");
    }
    api.Snapshot = new ApiSnapshot([], [], [new ApiPersonal { Id = 777, SubdivisionId = 200, FirstName = "New", LastName = "Delta", UpdatedAt = DateTimeOffset.UtcNow }]);
    var newEmployeeDelta = await service.RunAsync(updatedSince: new DateOnly(1962, 10, 1));
    await using (var db = await factory.CreateDbContextAsync())
      check(newEmployeeDelta.Status == SynchronizationStatus.Succeeded && await db.Personals.AnyAsync(p => p.ExternalId == 777),
        "bounded delta imports new employee in stored unchanged subdivision");
    await PostgresTests.ThrowsAsync<ArgumentException>(() => service.SaveSettingsAsync(DataManagementMode.Api, false, new(2, 0), overview.Settings.Version, apiHeadSubdivisionId: 0),
      check, "server rejects nonpositive head setting");
    permissions.Current = new("test-api-operator", false, [Guard.Core.Identity.Permissions.Synchronization.Run]);
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveSettingsAsync(DataManagementMode.Api, false, new(2, 0), overview.Settings.Version, apiHeadSubdivisionId: 200),
      check, "operator cannot change head setting on server");
    permissions.Current = new("test-api-root", true, []);
  }

  private sealed class ClientStub : IDirectoryApiClient
  {
    public Task<ApiSnapshot> ResolveDependenciesAsync(ApiSnapshot snapshot, IReadOnlySet<(int Type, int Code)> knownClassifiers,
      IReadOnlySet<long> knownSubdivisions, string baseUrl, string username, string password, CancellationToken ct) => Task.FromResult(snapshot);
    public Task PingAsync(string baseUrl, string username, string password, CancellationToken ct) => Task.CompletedTask;
    public ApiSnapshot Snapshot { get; set; } = ApiSynchronizationTests.Snapshot();
    public bool ThrowOnFetch { get; set; }
    public TaskCompletionSource? Pause { get; set; }
    public TaskCompletionSource? Started { get; set; }
    public Task<ApiSnapshot> FetchAsync(string baseUrl, int pageSize, CancellationToken ct, string? username = null, string? password = null, DateOnly? updatedSince = null, long? headSubdivisionId = null, Func<string, CancellationToken, Task>? phaseChanged = null) => FetchAsync(ct);
    public Task<ApiSnapshot> FetchDiagnosticAsync(string baseUrl, int pageSize, string username, string password, DateOnly updatedSince, CancellationToken ct, IProgress<string>? progress = null) => FetchAsync(ct);
    public async Task<ApiSnapshot> FetchAsync(CancellationToken ct)
    {
      ct.ThrowIfCancellationRequested();
      if (ThrowOnFetch) throw new HttpRequestException("Test failure");
      if (Pause != null) { Started!.TrySetResult(); await Pause.Task.WaitAsync(ct); }
      return Snapshot;
    }
  }
  private sealed class PermissionsStub : IPermissionService
  {
    public UserAccessSnapshot Current { get; set; } = new("test-api-root", true, []);
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(Current);
    public Task<UserAccessSnapshot> GetForUserAsync(string userId, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string permission, CancellationToken ct = default) => Task.FromResult(Current.Has(permission));
    public Task RequireAsync(string permission, CancellationToken ct = default) => Current.Has(permission) ? Task.CompletedTask : throw new UnauthorizedAccessException();
  }
  private sealed class AuditStub : IAuditService
  {
    public void LogIdentityEvent(AuditEventType eventType, string? userName, string? ipAddress = null, string? details = null) { }
  }
}
