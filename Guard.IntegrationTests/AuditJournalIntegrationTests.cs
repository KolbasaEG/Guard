using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Logging;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Guard.Core.Services.Exports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using System.Text.Json;

internal static class AuditJournalIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    await using var db = await factory.CreateDbContextAsync();
    if (!db.Database.GetDbConnection().Database.StartsWith("guard_ip_test_", StringComparison.Ordinal))
      throw new InvalidOperationException("Audit checks require the runner's isolated database.");
    var upgrade = await File.ReadAllTextAsync("scripts/Upgrade-Logs.sql");
    var originalDirectory = Environment.CurrentDirectory;
    var root = Path.Combine(Path.GetTempPath(), "guard-audit-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    Environment.CurrentDirectory = root;
    try {
      var settingsBefore = await db.SynchronizationSettings.Select(e => e.ApiBaseUrl).SingleAsync();
      await db.GetService<IMigrator>().MigrateAsync("20261017160000_AddPersonalActiveNameIndex");
      await db.GetService<IMigrator>().MigrateAsync();
      check(!await db.AuditEvents.AnyAsync() && await db.SynchronizationSettings.Select(e => e.ApiBaseUrl).SingleAsync() == settingsBefore,
        "audit migration down/up preserves existing application data and restores empty queue");
      await db.AuditSettings.ExecuteUpdateAsync(s => s.SetProperty(e => e.Configured, true).SetProperty(e => e.Mode, AuditMode.Siem)
        .SetProperty(e => e.MaxQueueAgeMinutes, 525600).SetProperty(e => e.MaxQueueBytes, 1073741824L).SetProperty(e => e.MaxQueueCount, 100000)
        .SetProperty(e => e.Host, "localhost"));

      using var health = new AuditHealth();
      using var journal = new AuditJournal(db.Database.GetConnectionString()!, new InternalLogWriter(Path.Combine(root, "diagnostics")), health);
      using var audit = new AuditService(new LoggerConfiguration().CreateLogger(), journal);
      var entry = AuditEventData.Create(AuditEventType.LoginFailed, "audit-test", "192.0.2.1", "original");
      await audit.RecordAsync(entry); await audit.RecordAsync(entry with { Details = "duplicate must not replace" });
      check(await db.AuditEvents.CountAsync() == 1 && (await db.AuditEvents.AsNoTracking().SingleAsync()).Details == "original",
        "immutable EventId makes repeated persistence idempotent without replacing the payload");
      var longActor = new string('a', 300) + "@example.com";
      using (AuditOperationContext.Push(new(null, "192.0.2.2", "anonymous-login", "anonymous")))
        audit.LogIdentityEvent(AuditEventType.LoginFailed, longActor);
      var oversized = await db.AuditEvents.AsNoTracking().SingleAsync(e => e.CorrelationId == "anonymous-login");
      check(oversized.Actor == null && oversized.AttemptedAccount!.Length <= 256 && oversized.AttemptedAccount.Contains("...[", StringComparison.Ordinal) && oversized.ActorKind == "anonymous" && health.StorageFailures == 0,
        "oversized attempted login is retained with bounded identity and explicit truncation marker");
      using (AuditOperationContext.Push(new(null, "192.0.2.3", "nul-login", "anonymous")))
        audit.LogIdentityEvent(AuditEventType.LoginFailed, "a\0b@example.com");
      check(await db.AuditEvents.AnyAsync(e => e.CorrelationId == "nul-login" && e.AttemptedAccount == "a\\0b@example.com") && health.StorageFailures == 0,
        "NUL in attempted identity cannot suppress persisted login-failure audit");

      var transport = new Delivery { Fail = true };
      var processor = new AuditDeliveryProcessor(factory, transport, health, new InternalLogWriter(Path.Combine(root, "diagnostics")));
      check(await processor.ProcessAsync(default) == 3, "unavailable SIEM processes persisted events without deleting them");
      var delayed = await db.AuditEvents.AsNoTracking().ToListAsync();
      check(delayed.All(e => e.SentAtUtc == null && e.Attempts == 1 && e.LastError == nameof(IOException) && e.LeaseToken == null && e.NextAttemptAtUtc > DateTime.UtcNow),
        "SIEM failure durably records attempt, retry time and released lease");
      check(await processor.ProcessAsync(default) == 0, "backoff prevents a busy retry loop");
      await db.AuditEvents.ExecuteUpdateAsync(s => s.SetProperty(e => e.NextAttemptAtUtc, DateTime.UtcNow.AddSeconds(-1)));
      transport.Fail = false;
      var restarted = new AuditDeliveryProcessor(factory, transport, health, new InternalLogWriter(Path.Combine(root, "diagnostics")));
      await restarted.ProcessAsync(default);
      check(await db.AuditEvents.CountAsync(e => e.SentAtUtc != null && e.Attempts == 2 && e.LastError == null) == 3 && transport.Ids.Contains(entry.EventId),
        "new processor after restart retries the same EventId and marks successful TLS writes");
      await db.AuditEvents.ExecuteDeleteAsync();

      var concurrent = AuditEventData.Create(AuditEventType.LoginSuccess, "audit-test");
      await audit.RecordAsync(concurrent);
      var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      var held = new Delivery { Observe = async _ => { entered.TrySetResult(); await release.Task; } };
      var first = new AuditDeliveryProcessor(factory, held, health, new InternalLogWriter(Path.Combine(root, "diagnostics"))).ProcessAsync(default);
      await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
      try {
        check(await restarted.ProcessAsync(default) == 0 && held.Ids.Count == 1,
          "second processor cannot claim an event while the first holds a valid lease");
      }
      finally { release.TrySetResult(); await first; }
      await db.AuditEvents.ExecuteUpdateAsync(s => s.SetProperty(e => e.SentAtUtc, (DateTime?)null)
        .SetProperty(e => e.LeaseToken, Guid.NewGuid()).SetProperty(e => e.LeaseUntilUtc, DateTime.UtcNow.AddSeconds(-1))
        .SetProperty(e => e.NextAttemptAtUtc, DateTime.UtcNow.AddSeconds(-1)));
      await restarted.ProcessAsync(default);
      check((await db.AuditEvents.AsNoTracking().SingleAsync()).SentAtUtc != null, "expired lease is recovered after a crashed sender");
      await db.AuditEvents.ExecuteDeleteAsync();

      var permissions = new Access();
      await using var services = new ServiceCollection().AddSingleton<IAuditService>(audit).BuildServiceProvider();
      var writes = new UnitOfWorkFactory(factory, permissions, services, NullLogger<UnitOfWork>.Instance);
      var person = new Personal { LastName = "SensitiveLast", FirstName = "SensitiveBefore" };
      await using (var work = await writes.CreateAsync()) {
        await work.BaseEntityRepository<Personal>().AddAsync(person); await work.SaveChangesAsync();
      }
      await using (var work = await writes.CreateAsync()) {
        var saved = (await work.BaseEntityRepository<Personal>().GetByIdAsync(person.Id))!;
        saved.FirstName = "SensitiveMiddle"; await work.SaveChangesAsync();
        saved.FirstName = "SensitiveAfter"; await work.SaveChangesAsync();
      }
      var personalEvents = await db.AuditEvents.AsNoTracking().Where(e => e.ObjectId == person.Id.ToString()).ToListAsync();
      check(personalEvents.Count == 3 && personalEvents.All(e => e.Actor == "audit-test") &&
        personalEvents.Last().Details!.Contains("FirstName") && personalEvents.All(e => !e.Details!.Contains("Sensitive")),
        "every personnel name edit records changed field names without sensitive old/new names");

      var classifier = new Classifier { Type = 991234, Code = 1, ClassifierName = "audit-fixture", Value = "audit", IsActive = true };
      await using (var work = await writes.CreateAsync()) {
        await work.BasicRepository<Classifier>().AddAsync(classifier); await work.SaveChangesAsync();
        classifier.IsActive = false; await work.SaveChangesAsync();
      }
      check(await db.AuditEvents.AnyAsync(e => e.ObjectType == "Classifiers" && e.Details!.Contains("IsActive")),
        "classifier activation change is retained in audit history");
      var beforeRollback = await db.AuditEvents.CountAsync();
      await using (var work = await writes.CreateAsync()) {
        await work.BeginTransactionAsync();
        await work.BasicRepository<Classifier>().AddAsync(new() { Type = 991234, Code = 2, ClassifierName = "rolled-back", Value = "rolled-back" });
        await work.SaveChangesAsync();
        check(await db.AuditEvents.CountAsync() == beforeRollback, "uncommitted SQL save emits no successful durable audit");
        await work.RollbackTransactionAsync();
      }
      check(await db.AuditEvents.CountAsync() == beforeRollback && !await db.Classifiers.AnyAsync(e => e.Type == 991234 && e.Code == 2),
        "rolled back business write produces neither data nor a successful audit event");

      await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"AuditEvents\" RENAME TO \"AuditEventsOffline\"");
      try {
        await using var work = await writes.CreateAsync();
        await work.BasicRepository<Classifier>().AddAsync(new() { Type = 991234, Code = 3, ClassifierName = "audit-offline", Value = "audit-offline" });
        await PostgresTests.ThrowsAsync<AuditUnavailableException>(() => work.SaveChangesAsync(), check, "failed local audit rejects the business operation");
        check(!await db.Classifiers.AnyAsync(e => e.Type == 991234 && e.Code == 3) && health.StorageFailures > 0,
          "failed local audit storage rolls back business data and signals independent diagnostics");
      }
      finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"AuditEventsOffline\" RENAME TO \"AuditEvents\""); }

      var subdivision = new Subdivision { Name = "audit-subdivision", Path = "/991234/", CreatedBy = "audit-test" };
      db.Subdivisions.Add(subdivision); await db.SaveChangesAsync();
      var ipAccess = new IpAccess();
      var ips = new IpAddressService(factory, ipAccess, NullLogger<IpAddressService>.Instance, audit);
      var ipId = await ips.CreateAsync(new("198.18.250.254", "audit", subdivision.Id));
      check(await db.AuditEvents.AnyAsync(e => e.ObjectType == "IpAddresses" && e.ObjectId == ipId.ToString()),
        "direct factory IP creation commits its audit atomically");
      var linked = await db.Personals.SingleAsync(e => e.Id == person.Id); linked.SubdivisionId = subdivision.Id; await db.SaveChangesAsync();
      var assignments = new PersonalIpService(factory, ipAccess, NullLogger<PersonalIpService>.Instance, audit);
      await assignments.UpdateAsync(person.Id, [ipId]);
      check(await db.AuditEvents.AnyAsync(e => e.ObjectType == "PersonalIpAddresses" && e.ObjectId!.Contains(ipId.ToString())),
        "IP access assignment emits an independent relation audit event");
      var beforeDenied = await db.AuditEvents.CountAsync();
      await PostgresTests.ThrowsAsync<EntityRuleException>(() => assignments.UpdateAsync(person.Id, [Guid.NewGuid()]), check, "invalid IP assignment is rejected");
      check(await db.AuditEvents.CountAsync() == beforeDenied, "rejected assignment emits no successful change audit");
      await PostgresTests.ThrowsAsync<IOException>(() => new IpAddressService(factory, ipAccess, NullLogger<IpAddressService>.Instance, new ThrowAudit())
        .UpdateAsync(new(ipId, "198.18.250.254", "updated-after-audit-failure", subdivision.Id)), check, "IP update rejects a failed mandatory audit adapter");
      check(!await db.IpAddresses.AnyAsync(e => e.Id == ipId && e.Description == "updated-after-audit-failure"),
        "IP update rolls back when audit adapter throws");

      var search = new AuditSearchService(factory, permissions, health);
      var page1 = await search.SearchAsync(new(Take: 2)); var page2 = await search.SearchAsync(new(Skip: 2, Take: 2));
      check(page1.Count == beforeDenied && !page1.Items.Select(e => e.EventId).Intersect(page2.Items.Select(e => e.EventId)).Any(),
        "audit search counts first and pages by deterministic unique keys");
      var details = await search.GetAsync(page1.Items[0].EventId);
      check(details?.Entry.EventId == page1.Items[0].EventId && details.SchemaVersion == 2, "audit details load by persistent event ID");
      permissions.Root = false;
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => search.SearchAsync(new()), check, "non-Root cannot search audit even with a copied permission");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => search.GetAsync(entry.EventId), check, "non-Root cannot load another audit event by ID");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => search.GetHealthAsync(), check, "non-Root cannot inspect queue health");
      permissions.Root = true; permissions.RevokeAfter = permissions.Calls + 1;
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => search.SearchAsync(new()), check, "audit read discards results after Root is revoked during query");
      permissions.RevokeAfter = int.MaxValue;
      await PostgresTests.ThrowsAsync<ArgumentException>(() => search.SearchAsync(new(OrderBy: "Details")), check, "audit does not allow sorting hidden payload fields");
      await db.AuditEvents.ExecuteDeleteAsync();

      // Проверка точного SQL обновления существующей БД логов, с двумя одинаковыми историческими строками.
      await db.Database.ExecuteSqlRawAsync("CREATE TABLE logs (timestamp timestamp NOT NULL, level varchar(50), layer varchar(50), message text, message_template text, exception text, properties jsonb, user_id varchar(250), ip_address varchar(50))");
      try {
        await db.Database.ExecuteSqlRawAsync("INSERT INTO logs (timestamp,message) VALUES ('2026-01-01','duplicate'),('2026-01-01','duplicate')");
        var sql = string.Join('\n', upgrade.Split('\n').Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        foreach (var statement in sql.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
          await db.Database.ExecuteSqlRawAsync(statement);
        var logFactory = new LogsFactory(db.Database.GetConnectionString()!);
        await using var logs = logFactory.CreateDbContext();
        var historical = await logs.Logs.AsNoTracking().ToListAsync();
        check(historical.Count == 2 && historical.Select(e => e.Id).Distinct().Count() == 2 && historical.All(e => e.Message == "duplicate"),
          "logs schema upgrade preserves duplicate historical events and assigns distinct persistent IDs");
        using (var dynamic = new DynamicLoggerManager(db.Database.GetConnectionString()!, new Serilog.Core.LoggingLevelSwitch())) {
          dynamic.ApplyConfiguration(100, Serilog.Events.LogEventLevel.Information);
          var existingLogger = dynamic.Logger.ForContext("UserId", "dynamic-fixture");
          existingLogger.Information("dynamic-before {Sequence}", 1);
          dynamic.ApplyConfiguration(200, Serilog.Events.LogEventLevel.Information);
          existingLogger.Information("dynamic-after {Sequence}", 2);
        }
        var dynamicEvents = await logs.Logs.AsNoTracking().Where(e => e.UserId == "dynamic-fixture").ToListAsync();
        check(dynamicEvents.Count == 2 && dynamicEvents.All(e => e.Properties!.Contains("Sequence")),
          "real PostgreSQL sink flushes both generations when configuration switches and host stops");
        var localCopies = Directory.GetFiles(Path.Combine(root, "logs"), "app-fallback-*.txt").Select(File.ReadAllText).ToArray();
        check(localCopies.Any(text => text.Contains("dynamic-before") && text.Contains("dynamic-after")),
          "Information events retain a local structured copy independently of PostgreSQL");
        await db.Database.ExecuteSqlRawAsync("DELETE FROM logs");
        var old = DateTime.UtcNow.AddYears(-1).AddDays(-1);
        var recent = DateTime.UtcNow.AddYears(-1).AddDays(1);
        await audit.RecordAsync(AuditEventData.Create(AuditEventType.LoginSuccess, "old-pending") with { OccurredAtUtc = old });
        await audit.RecordAsync(AuditEventData.Create(AuditEventType.LoginSuccess, "old-sent") with { OccurredAtUtc = old });
        await audit.RecordAsync(AuditEventData.Create(AuditEventType.LoginSuccess, "recent-sent") with { OccurredAtUtc = recent });
        await db.AuditEvents.Where(e => e.Actor != "old-pending").ExecuteUpdateAsync(s => s.SetProperty(e => e.SentAtUtc, DateTime.UtcNow));
        logs.Logs.AddRange(new LogEntry { Timestamp = old, Message = "old" }, new LogEntry { Timestamp = recent, Message = "recent" });
        await logs.SaveChangesAsync();
        var retentionFiles = Path.Combine(root, "retention"); Directory.CreateDirectory(retentionFiles);
        var oldFile = Path.Combine(retentionFiles, "app-fallback-old.txt"); await File.WriteAllTextAsync(oldFile, "old"); File.SetLastWriteTimeUtc(oldFile, old);
        var unrelated = Path.Combine(retentionFiles, "keep.txt"); await File.WriteAllTextAsync(unrelated, "keep"); File.SetLastWriteTimeUtc(unrelated, old);
        await new LogRetention(factory, logFactory, retentionFiles).CleanAsync(default);
        check(await db.AuditEvents.CountAsync() == 2 && await db.AuditEvents.AnyAsync(e => e.Actor == "old-pending") &&
          await db.AuditEvents.AnyAsync(e => e.Actor == "recent-sent"), "annual retention removes delivered old audit and preserves pending beyond one year");
        check(await logs.Logs.CountAsync() == 1 && (await logs.Logs.SingleAsync()).Message == "recent" && !File.Exists(oldFile) && File.Exists(unrelated),
          "annual retention cleans old technical rows and owned log files without deleting unrelated files");
        await File.WriteAllTextAsync(oldFile, "old-again"); File.SetLastWriteTimeUtc(oldFile, old);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE logs RENAME TO logs_offline");
        try {
          using var cleanupHealth = new AuditHealth();
          await new LogRetention(factory, logFactory, retentionFiles, health: cleanupHealth).CleanAsync(default);
          check(!File.Exists(oldFile) && cleanupHealth.CleanupError?.Contains("Technical:") == true,
            "technical storage failure is visible and does not prevent independent cleanup of old files");
        } finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE logs_offline RENAME TO logs"); }
        await db.AuditSettings.ExecuteUpdateAsync(s => s.SetProperty(e => e.Mode, AuditMode.Local));
        await audit.RecordAsync(AuditEventData.Create(AuditEventType.LoginSuccess, "old-local") with { OccurredAtUtc = old });
        await new LogRetention(factory, logFactory, retentionFiles).CleanAsync(default);
        check(!await db.AuditEvents.AnyAsync(e => e.Actor == "old-local") && await db.AuditEvents.AnyAsync(e => e.Actor == "old-pending"),
          "Local retention removes old local events while retaining the paused mandatory SIEM queue");
      }
      finally { await db.Database.ExecuteSqlRawAsync("DROP TABLE logs"); }
      await db.AuditEvents.ExecuteDeleteAsync();
      await db.Personals.Where(e => e.Id == person.Id).ExecuteDeleteAsync();
      await db.IpAddresses.Where(e => e.Id == ipId).ExecuteDeleteAsync();
      await db.Subdivisions.Where(e => e.Id == subdivision.Id).ExecuteDeleteAsync();
      await db.Classifiers.Where(e => e.Type == 991234).ExecuteDeleteAsync();
      await db.AuditSettings.ExecuteUpdateAsync(s => s.SetProperty(e => e.Mode, AuditMode.Local));
    }
    finally { Environment.CurrentDirectory = originalDirectory; Directory.Delete(root, recursive: true); }
  }

  private sealed class LogsFactory(string connection) : IDbContextFactory<LogsDbContext>
  { public LogsDbContext CreateDbContext() => new(new DbContextOptionsBuilder<LogsDbContext>().UseNpgsql(connection).Options); }
  private sealed class Delivery : IAuditTransport
  {
    public bool Fail; public List<Guid> Ids = []; public Func<AuditEventRecord, Task>? Observe;
    public async Task SendAsync(AuditEventRecord entry, CancellationToken ct) {
      Ids.Add(entry.EventId); if (Fail) throw new IOException(); if (Observe != null) await Observe(entry);
    }
  }
  private sealed class ThrowAudit : IAuditService
  { public Task RecordAsync(Guard.Core.Services.AuditEventData entry, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); LogIdentityEvent(entry.EventType, entry.Actor, entry.ClientIp, entry.Details); return Task.CompletedTask; }
    public void LogIdentityEvent(AuditEventType type, string? actor, string? ip = null, string? details = null) => throw new IOException(); }
  private sealed class IpAccess : IIpManagementAccessService
  { public Task<IpManagementScope> GetScopeAsync(bool write, CancellationToken ct = default) => Task.FromResult(new IpManagementScope("audit-test", true, [])); }
  private sealed class Access : IPermissionService
  {
    public bool Root = true; public int Calls; public int RevokeAfter = int.MaxValue;
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(new UserAccessSnapshot("audit-test", Root && ++Calls <= RevokeAfter, [Permissions.Audit.Read]));
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string code, CancellationToken ct = default) => Task.FromResult(Root);
    public Task RequireAsync(string code, CancellationToken ct = default) => Root ? Task.CompletedTask : Task.FromException(new UnauthorizedAccessException());
  }
}
