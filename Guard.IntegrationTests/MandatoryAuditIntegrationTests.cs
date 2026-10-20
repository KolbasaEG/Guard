using System.Data.Common;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Logging;
using Guard.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Guard.Infrastructure;
using Guard;
using Npgsql;
using Serilog;

internal static class MandatoryAuditIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> bare, Action<bool, string> check)
  {
    await using var inspect = await bare.CreateDbContextAsync();
    var connection = new NpgsqlConnectionStringBuilder(inspect.Database.GetConnectionString());
    if (!connection.Database!.StartsWith("guard_ip_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Only the isolated runner database is allowed.");
    connection.ApplicationName = "guard-mandatory-" + Guid.NewGuid().ToString("N");
    var directory = Path.Combine(Path.GetTempPath(), connection.ApplicationName);
    using var health = new AuditHealth();
    using var journal = new AuditJournal(connection.ConnectionString, new InternalLogWriter(directory), health);
    using var audit = new AuditService(new LoggerConfiguration().CreateLogger(), journal);
    IDbContextFactory<ApplicationDbContext> factory = new AuditedFactory(connection.ConnectionString, new AuditPersistence(journal));
    var session = new Session("root", "test");
    var administration = new AuditAdministrationService(session, new PermissionReader(bare), bare, journal, health);
    try {
      await inspect.AuditSettings.ExecuteUpdateAsync(s => s.SetProperty(c => c.Configured, false));
      var initialCount = await inspect.AuditEvents.CountAsync();
      var initial = await ReadStateWithoutRejectionAsync();
      check(!initial.Available && initial.Code == "NotConfigured" && initial.LastStorageSuccessUtc.HasValue,
        "first startup reports missing Root configuration separately from a storage failure");
      check(await inspect.AuditEvents.CountAsync() == initialCount, "unconfigured readiness probe does not retain a read event");
      var processorRequested = 0;
      await using (var workerServices = new ServiceCollection().AddSingleton(journal)
        .AddScoped<Guard.Core.Services.Exports.ExportProcessor>(_ => {
          Interlocked.Increment(ref processorRequested);
          throw new InvalidOperationException("Export must wait for audit configuration.");
        }).BuildServiceProvider()) {
        using var worker = new Guard.Core.Services.Exports.ExportWorker(workerServices.GetRequiredService<IServiceScopeFactory>(),
          NullLogger<Guard.Core.Services.Exports.ExportWorker>.Instance);
        var checkedBefore = health.LastStorageSuccessUtc;
        await worker.StartAsync(default);
        try {
          using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(5));
          while (health.LastStorageSuccessUtc == checkedBefore) await Task.Delay(10, wait.Token);
        } finally { await worker.StopAsync(default); }
        check(processorRequested == 0 && health.LastStorageSuccessUtc != checkedBefore,
          "startup export worker checks audit and waits without starting ordinary processing before Root configuration");
      }
      await PostgresTests.ThrowsAsync<AuditUnavailableException>(() => journal.RequireWorkingAsync(), check,
        "first startup still refuses ordinary operations until Root confirms audit mode");
      await SaveMode(AuditMode.Local);
      await journal.RequireWorkingAsync();
      await audit.RecordAsync(AuditEventData.Create(AuditEventType.LoginSuccess, "root") with { ObjectType = "Account", ObjectId = "user" });
      check(await inspect.AuditEvents.AnyAsync(e => e.Actor == "root" && e.ObjectId == "user" && !e.DeliveryRequired),
        "local mode persists separate initiator and subject without an SIEM obligation");
      var old = AuditEventData.Create(AuditEventType.LoginSuccess, "root") with { EventId = Guid.NewGuid() };
      inspect.AuditEvents.Add(new() { EventId = old.EventId, OccurredAtUtc = DateTime.UtcNow.AddYears(-2),
        EventType = old.EventType, ActorKind = "user", Outcome = "success", DeliveryRequired = true,
        QueuedAtUtc = DateTime.UtcNow.AddYears(-2), NextAttemptAtUtc = DateTime.UtcNow, PayloadBytes = 2000000 });
      await inspect.SaveChangesAsync();
      check((await journal.GetStateAsync()).Available, "local mode ignores age and volume of a preserved old mandatory queue");
      var transport = new Transport();
      var sender = new AuditDeliveryProcessor(bare, transport, health, new InternalLogWriter(directory));
      check(await sender.ProcessAsync(default) == 0 && transport.Ids.Count == 0, "local mode pauses the preserved SIEM queue");
      await SaveMode(AuditMode.Siem);
      check(!(await journal.GetStateAsync()).Available, "enabling SIEM reinstates the limits of the preserved old queue");
      await SaveMode(AuditMode.Local);
      check((await journal.GetStateAsync()).Available && await inspect.AuditEvents.AnyAsync(e => e.EventId == old.EventId),
        "Root can recover to local mode without deleting the mandatory queue");
      await inspect.AuditEvents.ExecuteDeleteAsync();
      await SaveMode(AuditMode.Siem, count: 2);
      await inspect.AuditEvents.ExecuteDeleteAsync();
      var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ => {
        try { await audit.RecordAsync(AuditEventData.Create(AuditEventType.LoginSuccess, "root")); return true; }
        catch (AuditUnavailableException) { return false; }
      }));
      check(outcomes.Count(v => v) == 2 && await inspect.AuditEvents.CountAsync(e => e.DeliveryRequired && e.SentAtUtc == null) == 2,
        "concurrent writers cannot overrun the queue count budget");
      await using (var db = await factory.CreateDbContextAsync()) {
        db.Classifiers.Add(Classifier(1));
        await PostgresTests.ThrowsAsync<AuditUnavailableException>(() => db.SaveChangesAsync(), check, "a write requiring another event is rejected at the queue boundary");
      }
      check(!await inspect.Classifiers.AnyAsync(c => c.Type == 992345 && c.Code == 1), "queue reservation failure rolls back the actual business insert");
      await inspect.AuditEvents.ExecuteUpdateAsync(s => s.SetProperty(e => e.QueuedAtUtc, DateTime.UtcNow.AddMinutes(-2)));
      check((await ReadStateWithoutRejectionAsync()).Code == "QueueAgeExceeded", "trusted queued time exceeding the age limit blocks work");
      await sender.ProcessAsync(default);
      check((await journal.GetStateAsync()).Available && await inspect.AuditDeliveryAttempts.CountAsync() == 2,
        "delivery drains the queue and restores availability automatically with persistent attempt history");

      await SaveMode(AuditMode.Siem, count: 0);
      await inspect.AuditEvents.ExecuteDeleteAsync();
      await audit.RecordAsync(AuditEventData.Create(AuditEventType.LoginSuccess, "root") with { OccurredAtUtc = DateTime.UtcNow.AddYears(-2) });
      check((await journal.GetStateAsync()).Available, "a historical occurrence does not falsify the new trusted queue age");
      var change = await administration.GetConfigurationAsync(); var previousVersion = change.Version;
      change.Host = "new-siem.example.test";
      await PostgresTests.ThrowsAsync<ArgumentException>(() => administration.SaveAsync(change, false), check, "retargeting pending events requires explicit Root confirmation");
      check((await administration.GetConfigurationAsync()).Version == previousVersion, "unconfirmed destination transfer does not change settings");
      await administration.SaveAsync(change, true);
      var moved = await administration.GetConfigurationAsync();
      check(await inspect.AuditEvents.Where(e => e.DeliveryRequired && e.SentAtUtc == null).AllAsync(e => e.DestinationVersion == moved.Version),
        "confirmed retargeting changes the queue destination version atomically");
      var held = new HeldTransport();
      var activeSender = new AuditDeliveryProcessor(bare, held, health, new InternalLogWriter(directory));
      var sending = activeSender.ProcessAsync(default);
      await held.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
      try {
        await PostgresTests.ThrowsAsync<InvalidOperationException>(() => SaveMode(AuditMode.Local), check,
          "Local cannot be confirmed while an SIEM attempt can still start sending");
        check((await administration.GetConfigurationAsync()).Mode == AuditMode.Siem,
          "rejected mode switch leaves active mode unchanged until sender completes");
      } finally { held.Release.TrySetResult(); await sending; }
      await SaveMode(AuditMode.Local);

      var transactionCount = await inspect.AuditEvents.CountAsync();
      await using (var db = await factory.CreateDbContextAsync()) {
        await using var tx = await db.Database.BeginTransactionAsync();
        db.Classifiers.Add(Classifier(2)); await db.SaveChangesAsync();
        check(await db.AuditEvents.CountAsync() > transactionCount && await inspect.AuditEvents.CountAsync() == transactionCount,
          "the mandatory event shares business transaction visibility");
        await tx.RollbackAsync();
      }
      check(!await inspect.Classifiers.AnyAsync(c => c.Type == 992345 && c.Code == 2) && await inspect.AuditEvents.CountAsync() == transactionCount,
        "rollback removes both business insert and staged audit");

      await inspect.Database.ExecuteSqlRawAsync("ALTER TABLE \"AuditEvents\" RENAME TO \"AuditEventsOffline\"");
      try {
        var config = await administration.GetConfigurationAsync();
        config.RetentionYears = 2;
        await PostgresTests.ThrowsAsync<PostgresException>(() => administration.SaveAsync(config, false), check,
          "configuration cannot commit without its mandatory event");
        check((await administration.GetConfigurationAsync()).RetentionYears == 1, "configuration rolls back together with failed audit");
        await using var db = await factory.CreateDbContextAsync(); db.Classifiers.Add(Classifier(3));
        await PostgresTests.ThrowsAsync<AuditUnavailableException>(() => db.SaveChangesAsync(), check, "a missing audit table fails closed");
        check(!(await journal.GetStateAsync()).Available && !await inspect.Classifiers.AnyAsync(c => c.Type == 992345 && c.Code == 3),
          "missing audit storage prevents both data commit and ordinary availability");
        var productionServices = new ServiceCollection().AddLogging();
        productionServices.AddSingleton<ICurrentSession>(session); productionServices.AddSingleton<IAuditService>(audit);
        productionServices.AddGuardInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
          ["ConnectionStrings:DefaultConnection"] = connection.ConnectionString,
          ["ConnectionStrings:LogsConnection"] = connection.ConnectionString
        }).Build(), false);
        await using var provider = productionServices.BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var account = (await manager.FindByIdAsync("root"))!; var prior = account.LockoutEnabled;
        await PostgresTests.ThrowsAsync<AuditUnavailableException>(() => manager.SetLockoutEnabledAsync(account, !prior), check,
          "actual production Identity store cannot save a service counter setting without local audit");
        check(await inspect.Users.Where(u => u.Id == "root").Select(u => u.LockoutEnabled).SingleAsync() == prior,
          "production scoped store rolls back the unlogged Identity change");
      } finally { await inspect.Database.ExecuteSqlRawAsync("ALTER TABLE \"AuditEventsOffline\" RENAME TO \"AuditEvents\""); }
      check((await journal.GetStateAsync()).Available, "successful storage validation restores local mode after repair");
      var countBeforeProbe = await inspect.AuditEvents.CountAsync();
      await inspect.Database.ExecuteSqlRawAsync("""
        CREATE FUNCTION mandatory_audit_commit_fault() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'Synthetic deferred commit failure'; END $$;
        CREATE CONSTRAINT TRIGGER mandatory_audit_commit_fault AFTER INSERT ON "AuditEvents"
          DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION mandatory_audit_commit_fault();
        """);
      try {
        check(!(await journal.GetStateAsync()).Available, "storage check detects a failure deferred until transaction commit");
      } finally {
        await inspect.Database.ExecuteSqlRawAsync("DROP TRIGGER mandatory_audit_commit_fault ON \"AuditEvents\"; DROP FUNCTION mandatory_audit_commit_fault()");
      }
      check((await journal.GetStateAsync()).Available && await inspect.AuditEvents.CountAsync() == countBeforeProbe,
        "successful committed probe resumes work and does not retain audit events for reads");
      await inspect.AuditSettings.ExecuteUpdateAsync(s => s.SetProperty(c => c.Mode, (AuditMode)999));
      check((await ReadStateWithoutRejectionAsync()).Code == "InvalidConfiguration", "unknown persisted audit mode cannot silently become unaudited Local");
      await SaveMode(AuditMode.Local);

      var lost = new LoseCommitAcknowledgement();
      await using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql(connection.ConnectionString).AddInterceptors(lost).Options, new AuditPersistence(journal))) {
        db.Classifiers.Add(Classifier(4));
        await PostgresTests.ThrowsAsync<OperationCommitUnknownException>(() => db.SaveChangesAsync(), check,
          "lost commit acknowledgement is not reported as a proven rollback");
      }
      var committedId = (await inspect.Classifiers.AsNoTracking().SingleAsync(c => c.Type == 992345 && c.Code == 4)).Id.ToString();
      check(await inspect.AuditEvents.AnyAsync(e => e.ObjectType == "Classifiers" && e.ObjectId == committedId),
        "after lost acknowledgement the database can contain both the actual change and its event");

      await CheckRevocationAsync();
      await CheckPasswordRollbackAsync();
      await CheckReadRecheckAsync();
      await CheckTrustedActorAsync();
      session.RecoveryOnly = true;
      await administration.RequireRootAsync();
      await using var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider();
      var policy = new AccountPolicyService(bare, services, audit, new AccessChangeNotifier(NullLogger<AccessChangeNotifier>.Instance));
      var permissions = new PermissionService(session, new PermissionReader(bare), audit, policy, journal);
      await PostgresTests.ThrowsAsync<AuditUnavailableException>(() => permissions.GetCurrentAsync(), check,
        "recovery-only session cannot gain ordinary access even after repair");
      var search = new AuditSearchService(bare, permissions, health, administration);
      check((await search.SearchAsync(new(Take: 1))).Count > 0, "verified Root can inspect the diagnostic journal through the recovery-only contract");
      session.RecoveryOnly = false;
    }
    finally {
      await inspect.Classifiers.Where(c => c.Type == 992345).ExecuteDeleteAsync();
      await SaveMode(AuditMode.Local);
      await inspect.AuditEvents.ExecuteDeleteAsync();
      if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    async Task<AuditSystemState> ReadStateWithoutRejectionAsync() {
      var rejections = 0;
      void Observe(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs args) {
        if (args.Exception is AuditUnavailableException) Interlocked.Increment(ref rejections);
      }
      AppDomain.CurrentDomain.FirstChanceException += Observe;
      AuditSystemState state;
      try { state = await journal.GetStateAsync(); }
      finally { AppDomain.CurrentDomain.FirstChanceException -= Observe; }
      check(rejections == 0, $"readiness reports {state.Code} without throwing an expected audit rejection under a debugger");
      return state;
    }
    async Task SaveMode(AuditMode mode, int count = 0) {
      var configuration = await administration.GetConfigurationAsync();
      configuration.Mode = mode; configuration.RetentionYears = 1; configuration.MaxQueueAgeMinutes = 1;
      configuration.MaxQueueBytes = 1048576; configuration.MaxQueueCount = count;
      if (configuration.Host.Length == 0) configuration.Host = "localhost";
      await administration.SaveAsync(configuration, true);
    }
    async Task CheckRevocationAsync() {
      var id = "mandatory-root-" + Guid.NewGuid().ToString("N");
      var role = await inspect.Roles.Where(r => r.NormalizedName == "ROOT").Select(r => r.Id).SingleAsync();
      inspect.Users.Add(new() { Id = id, UserName = id, SecurityStamp = "valid" });
      inspect.UserRoles.Add(new() { UserId = id, RoleId = role }); await inspect.SaveChangesAsync();
      var otherSession = new Session(id, "valid");
      var other = new AuditAdministrationService(otherSession, new PermissionReader(bare), bare, journal, health);
      var configuration = await other.GetConfigurationAsync(); var version = configuration.Version; configuration.RetentionYears = 3;
      await using var revoker = await bare.CreateDbContextAsync(); await using var tx = await revoker.Database.BeginTransactionAsync();
      await revoker.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)");
      var saving = other.SaveAsync(configuration, false);
      try {
        var observed = false;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        while (!observed) {
          timeout.Token.ThrowIfCancellationRequested();
          observed = await inspect.Database.SqlQuery<bool>($"SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE application_name={connection.ApplicationName} AND wait_event_type='Lock') AS \"Value\"").SingleAsync(timeout.Token);
          if (!observed) await Task.Delay(20, timeout.Token);
        }
        check(observed, "Root settings save is deterministically waiting for the security transaction lock");
        await revoker.UserRoles.Where(r => r.UserId == id).ExecuteDeleteAsync();
        await revoker.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityStamp, "revoked"));
        await tx.CommitAsync();
        await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => saving, check, "Root revoked while save waits cannot change audit configuration");
        check((await administration.GetConfigurationAsync()).Version == version, "revoked Root leaves the configuration version unchanged");
      }
      finally { await tx.DisposeAsync(); try { await saving; } catch (UnauthorizedAccessException) { } await inspect.Users.Where(u => u.Id == id).ExecuteDeleteAsync(); }
    }
    async Task CheckPasswordRollbackAsync() {
      var services = new ServiceCollection().AddLogging();
      services.AddScoped(_ => factory.CreateDbContext());
      services.AddSingleton<IAuditService>(audit);
      services.AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>().AddUserManager<PolicyUserManager>();
      await using var provider = services.BuildServiceProvider();
      var id = "mandatory-password-" + Guid.NewGuid().ToString("N");
      await using (var scope = provider.CreateAsyncScope()) {
        var manager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        check((await manager.CreateAsync(new() { Id = id, UserName = id }, "Original123!pass")).Succeeded,
          "password rollback fixture uses actual Identity store and mandatory audit persistence");
      }
      var before = await inspect.Users.AsNoTracking().SingleAsync(u => u.Id == id);
      await inspect.Database.ExecuteSqlRawAsync("""
        CREATE FUNCTION mandatory_password_audit_fault() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN IF NEW."EventType"=4 AND NEW."ObjectId" LIKE 'mandatory-password-%' THEN
          RAISE EXCEPTION 'Synthetic password semantic audit failure'; END IF; RETURN NEW; END $$;
        CREATE TRIGGER mandatory_password_audit_fault BEFORE INSERT ON "AuditEvents"
          FOR EACH ROW EXECUTE FUNCTION mandatory_password_audit_fault();
        """);
      try {
        await using var scope = provider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var account = (await manager.FindByIdAsync(id))!;
        await PostgresTests.ThrowsAsync<AuditUnavailableException>(() => manager.ChangePasswordAsync(account, "Original123!pass", "Replacement123!pass"),
          check, "failure of semantic password audit rolls back actual Identity SQL");
        var after = await inspect.Users.AsNoTracking().SingleAsync(u => u.Id == id);
        check(after.PasswordHash == before.PasswordHash && after.SecurityStamp == before.SecurityStamp &&
          after.PasswordChangedAtUtc == before.PasswordChangedAtUtc && account.PasswordHash == before.PasswordHash,
          "failed password audit preserves hash, security stamp, date and tracked account values");
      }
      finally {
        await inspect.Database.ExecuteSqlRawAsync("DROP TRIGGER mandatory_password_audit_fault ON \"AuditEvents\"; DROP FUNCTION mandatory_password_audit_fault()");
      }
      await using (var scope = provider.CreateAsyncScope()) {
        var manager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        check((await manager.ChangePasswordAsync((await manager.FindByIdAsync(id))!, "Original123!pass", "Replacement123!pass")).Succeeded,
          "repaired audit permits password change");
      }
      check(await inspect.AuditEvents.AnyAsync(e => e.ObjectId == id && e.EventType == AuditEventType.PasswordChanged) &&
        await inspect.Users.AnyAsync(u => u.Id == id && u.SecurityStamp != before.SecurityStamp),
        "successful password change commits semantic event and new security stamp together");
      await inspect.Users.Where(u => u.Id == id).ExecuteDeleteAsync();
    }
    async Task CheckReadRecheckAsync() {
      var gate = new ReadAvailability();
      await using var services = new ServiceCollection().BuildServiceProvider();
      var policy = new AccountPolicyService(bare, services, audit, new AccessChangeNotifier(NullLogger<AccessChangeNotifier>.Instance));
      var permissions = new PermissionService(session, new PermissionReader(bare), audit, policy, gate);
      var interceptor = new BlockAfterQuery(gate);
      var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection.ConnectionString).AddInterceptors(interceptor).Options;
      var exports = new Guard.Core.Services.Exports.DataExportService(new ReadFactory(dbOptions), permissions, null!, null!, null!, audit, null!,
        NullLogger<Guard.Core.Services.Exports.DataExportService>.Instance);
      await PostgresTests.ThrowsAsync<AuditUnavailableException>(() => exports.GetActiveAsync(ExportArea.Personals), check,
        "audit becoming unavailable during an ordinary DTO query prevents returning materialized data");
      check(gate.Blocked && gate.Checks >= 2, "ordinary read rechecks audit after its database query");
    }
    async Task CheckTrustedActorAsync() {
      var registrations = new ServiceCollection().AddLogging();
      registrations.AddSingleton<ICurrentSession>(session); registrations.AddSingleton<IAuditService>(audit);
      registrations.AddGuardApplication();
      registrations.AddGuardInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["ConnectionStrings:DefaultConnection"] = connection.ConnectionString,
        ["ConnectionStrings:LogsConnection"] = connection.ConnectionString
      }).Build(), false);
      await using var provider = registrations.BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
      var roles = scope.ServiceProvider.GetRequiredService<IRoleAccessService>();
      var id = await roles.SaveAsync(new() { Name = "mandatory-actor-" + Guid.NewGuid().ToString("N") });
      try {
        check(await inspect.AuditEvents.AnyAsync(e => e.ObjectType == "AspNetRoles" && e.ObjectId == id && e.Actor == "root" && e.ActorKind == "user"),
          "automatic Identity field audit uses the server-verified operator even without ambient HTTP metadata");
      } finally { await roles.DeleteAsync(id); }
    }
  }
  private static Classifier Classifier(int code) => new() { Type = 992345, Code = code, ClassifierName = "mandatory-test", Value = "fixture" };
  private sealed class Session(string id, string stamp) : ICurrentSession {
    public bool RecoveryOnly;
    public Task<CurrentSession?> GetAsync(CancellationToken ct = default) => Task.FromResult<CurrentSession?>(new(id, stamp, RecoveryOnly));
  }
  private sealed class AuditedFactory(string connection, AuditPersistence persistence) : IDbContextFactory<ApplicationDbContext> {
    public ApplicationDbContext CreateDbContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options, persistence);
  }
  private sealed class Transport : IAuditTransport {
    public List<Guid> Ids = [];
    public Task SendAsync(AuditEventRecord entry, CancellationToken ct) { Ids.Add(entry.EventId); return Task.CompletedTask; }
  }
  private sealed class HeldTransport : IAuditTransport {
    public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task SendAsync(AuditEventRecord entry, CancellationToken ct) { Started.TrySetResult(); await Release.Task.WaitAsync(ct); }
  }
  private sealed class LoseCommitAcknowledgement : DbTransactionInterceptor {
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken ct = default)
      => throw new IOException("Synthetic lost commit acknowledgement");
  }
  private sealed class ReadAvailability : IOperationAvailability {
    public bool Blocked; public int Checks;
    public Task RequireWorkingAsync(CancellationToken ct = default) {
      Checks++; return Blocked ? Task.FromException(new AuditUnavailableException("QueueAgeExceeded", "synthetic")) : Task.CompletedTask;
    }
  }
  private sealed class BlockAfterQuery(ReadAvailability gate) : DbCommandInterceptor {
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken ct = default) {
      if (command.CommandText.Contains("\"ExportJobs\"")) gate.Blocked = true;
      return ValueTask.FromResult(result);
    }
  }
  private sealed class ReadFactory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext> {
    public ApplicationDbContext CreateDbContext() => new(options);
  }
}
