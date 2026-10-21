using Guard.Core.Contexts;
using Guard.Core.Logging;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Text;
using System.Text.Json;

internal static class TechnicalLoggingPostgresTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    await using var app = await factory.CreateDbContextAsync();
    var connection = app.Database.GetConnectionString()!;
    if (!app.Database.GetDbConnection().Database.StartsWith("guard_ip_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Isolated test database required");
    var root = Path.Combine(Path.GetTempPath(), "guard-technical-pg-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
    DatabaseInitializer.EnsureLogsDatabaseExists(connection);
    try {
      var runtime = new TechnicalLogRuntime(Path.Combine(root, "settings.json"));
      using var buffer = new TechnicalLogBuffer(runtime, Path.Combine(root, "technical-buffer"), () => long.MaxValue);
      var delivery = new TechnicalLogDelivery(connection);
      await using (var transactionDb = await factory.CreateDbContextAsync()) {
        var failures = new TechnicalLoggingReadinessTests.Capture<UnitOfWork>();
        using var noAudit = new ServiceCollection().BuildServiceProvider();
        using var unit = new UnitOfWork(transactionDb, noAudit, failures);
        transactionDb.SavingChanges += (_, _) => throw new IOException("synthetic-save-failure");
        await PostgresTests.ThrowsAsync<IOException>(() => unit.ExecuteInTransactionAsync(() => Task.CompletedTask), check, "save failure propagates through transaction wrappers");
        check(failures.Levels.Count(e => e == Microsoft.Extensions.Logging.LogLevel.Error) == 1 && transactionDb.Database.CurrentTransaction == null,
          "Save/Commit/Execute register one Error and release the failed transaction");
      }
      await using (var transactionDb = await factory.CreateDbContextAsync()) {
        var failures = new TechnicalLoggingReadinessTests.Capture<UnitOfWork>();
        using var noAudit = new ServiceCollection().BuildServiceProvider();
        using var unit = new UnitOfWork(transactionDb, noAudit, failures);
        using var cancel = new CancellationTokenSource();
        transactionDb.SavingChanges += (_, _) => { cancel.Cancel(); throw new OperationCanceledException(cancel.Token); };
        await PostgresTests.ThrowsAsync<OperationCanceledException>(() => unit.ExecuteInTransactionAsync(() => Task.CompletedTask, cancel.Token), check, "caller cancellation propagates through transaction wrappers");
        check(!failures.Levels.Contains(Microsoft.Extensions.Logging.LogLevel.Error) && transactionDb.Database.CurrentTransaction == null,
          "expected transaction cancellation rolls back without an Error storm");
      }
      using (var manager = new DynamicLoggerManager(connection, new LoggingLevelSwitch(), runtime, buffer)) {
        manager.ApplyConfiguration(100, LogEventLevel.Information);
        var log = manager.Logger.ForContext("SourceContext", "Guard.Test.Delivery").ForContext("CorrelationId", "pg-correlation");
        log.Information("Persisted before outage");
        log.Error("Repeated failure {Count}", 1); log.Error("Repeated failure {Count}", 2);
        manager.ApplyConfiguration(231, LogEventLevel.Warning);
        log.Information("Persisted after logger replacement");
      }
      var batch = buffer.ReadBatch();
      check(batch.Count == 4 && await app.Database.ExecuteSqlRawAsync("UPDATE logs SET message='must-still-be-empty'") == 0,
        "technical events are durable locally before asynchronous PostgreSQL delivery");
      var first = batch[0].Record with { Id = Guid.NewGuid() };
      var invalid = batch[1].Record with { Id = Guid.NewGuid(), Properties = "{" };
      await PostgresTests.ThrowsAsync<PostgresException>(() => delivery.DeliverAsync([first, invalid], default), check, "invalid record aborts an entire delivery transaction");
      check(await app.Database.ExecuteSqlRawAsync("DELETE FROM logs") == 0 && buffer.Health().PendingCount == 4, "failed delivery neither commits the first record nor acknowledges local pending records");
      await app.Database.ExecuteSqlRawAsync("ALTER TABLE logs RENAME COLUMN diagnostic_session_id TO incompatible_session_id");
      try {
        await PostgresTests.ThrowsAsync<PostgresException>(() => delivery.DeliverAsync(batch.Select(e => e.Record).ToArray(), default), check, "incompatible schema fails delivery rather than silently discarding fields");
        check(buffer.ReadBatch().Select(e => e.Record.Id).SequenceEqual(batch.Select(e => e.Record.Id)), "schema failure preserves exact queued identities");
      } finally { await app.Database.ExecuteSqlRawAsync("ALTER TABLE logs RENAME COLUMN incompatible_session_id TO diagnostic_session_id"); }
      await app.Database.ExecuteSqlRawAsync("""
        CREATE FUNCTION technical_test_delay() RETURNS trigger LANGUAGE plpgsql AS $$
          BEGIN IF NEW.message='synthetic-slow' THEN PERFORM pg_sleep(2); END IF; RETURN NEW; END $$;
        CREATE TRIGGER technical_test_delay_trigger BEFORE INSERT ON logs FOR EACH ROW EXECUTE FUNCTION technical_test_delay()
        """);
      try {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await PostgresTests.ThrowsAsync<OperationCanceledException>(() => delivery.DeliverAsync([first, first with { Id = Guid.NewGuid(), Message = "synthetic-slow" }], timeout.Token),
          check, "delivery observes cancellation during a delayed PostgreSQL INSERT");
        check(await app.Database.ExecuteSqlRawAsync("DELETE FROM logs") == 0, "cancelled delivery rolls back all records in its batch");
      } finally { await app.Database.ExecuteSqlRawAsync("DROP TRIGGER technical_test_delay_trigger ON logs; DROP FUNCTION technical_test_delay()"); }
      await app.Database.ExecuteSqlRawAsync("ALTER TABLE logs RENAME TO logs_offline");
      var audit = new TechnicalLoggingTests.AuditCapture();
      using var scopes = new ServiceCollection().AddSingleton<IAuditService>(audit).BuildServiceProvider();
      using var worker = new TechnicalLogWorker(runtime, buffer, delivery, scopes.GetRequiredService<IServiceScopeFactory>(), new InternalLogWriter(Path.Combine(root, "internal")));
      var oldFile = Path.Combine(root, "startup-old.json"); await File.WriteAllTextAsync(oldFile, "old"); File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-31));
      try {
        await worker.TickAsync(default);
        check(buffer.Health().DatabaseError != null && buffer.Health().PendingCount == 4 && !File.Exists(oldFile),
          "database outage retains backlog, reports degraded state and independently cleans expired technical files");
      } finally { await app.Database.ExecuteSqlRawAsync("ALTER TABLE logs_offline RENAME TO logs"); }
      await delivery.DeliverAsync(batch.Select(x => x.Record).ToArray(), default);
      await delivery.DeliverAsync(batch.Select(x => x.Record).ToArray(), default);
      var logsFactory = new LogsFactory(connection);
      await using var logs = logsFactory.CreateDbContext();
      check(await logs.Logs.CountAsync() == 4 && (await logs.Logs.AsNoTracking().ToListAsync()).All(e => e.CorrelationId == "pg-correlation" && e.Timestamp.Kind == DateTimeKind.Utc),
        "replay after a lost acknowledgement produces no duplicate and preserves UTC/correlation columns");
      buffer.Acknowledge(batch); await worker.TickAsync(default);
      check(buffer.Health().PendingCount == 0 && buffer.Health().DatabaseError == null && buffer.Health().LastDatabaseProbeUtc != null,
        "recovery drains queue and proves INSERT permission before reporting healthy database");
      await File.WriteAllTextAsync(Path.Combine(buffer.DirectoryPath, "000-corrupt.json"), "{broken-json");
      var invalidProperties = batch[0].Record with { Id = Guid.NewGuid(), Properties = "{" };
      await File.WriteAllBytesAsync(Path.Combine(buffer.DirectoryPath, "001-corrupt-properties.json"), JsonSerializer.SerializeToUtf8Bytes(invalidProperties));
      await File.WriteAllBytesAsync(Path.Combine(buffer.DirectoryPath, "002-corrupt-nul.json"), JsonSerializer.SerializeToUtf8Bytes(batch[0].Record with { Id = Guid.NewGuid(), Properties = "{\"value\":\"\\u0000\"}" }));
      // Имитируем повреждение файла, уже учтённого в очереди, через перезапуск владельца.
      buffer.Dispose();
      using (var restored = new TechnicalLogBuffer(runtime, buffer.DirectoryPath, () => long.MaxValue)) {
        restored.Emit(TechnicalLoggingTests.Event(LogEventLevel.Information, "Guard.ValidAfterCorruption"));
        var valid = restored.ReadBatch(); await delivery.DeliverAsync(valid.Select(e => e.Record).ToArray(), default); restored.Acknowledge(valid);
        check(valid.Count == 1 && restored.Health().QuarantinedCount == 3 && restored.Health().LostEvents == 3 &&
          restored.Health().Status == "Emergency" && Directory.GetFiles(buffer.DirectoryPath, "*.bad").Length == 3,
          "corrupt outer JSON, nested properties and NUL enter quota-counted quarantine while following valid events are delivered");
      }
      // Следующие проверки используют новый независимый буфер; прежний сохранён вместе с карантином.
      using var activeBuffer = new TechnicalLogBuffer(runtime, Path.Combine(root, "active-buffer"), () => long.MaxValue);
      var service = new TechnicalLogService(new TechnicalLoggingTests.Access(), new TechnicalLoggingTests.Available(), runtime, activeBuffer, audit, logsFactory);
      var groups = await service.ErrorsAsync();
      check(groups.Count == 1 && groups.Single().Count == 2 && groups.Single().Category == "Guard.Test.Delivery",
        "error grouping uses category/template fingerprint rather than variable values");
      var from = DateTime.UtcNow.AddHours(-1); var to = DateTime.UtcNow.AddMinutes(1);
      var file = await service.ExportAsync(new(from, to, "pg-correlation"));
      check(file.Count == 4 && !file.Truncated && Encoding.UTF8.GetString(file.Content).Contains("pg-correlation") &&
        audit.Events.Any(e => e.EventType == Guard.Core.Enums.AuditEventType.ExportRequested) &&
        audit.Events.Last().EventType == Guard.Core.Enums.AuditEventType.ExportArchived, "bounded diagnostic export is authorized, filtered and audited");
      await app.Database.ExecuteSqlRawAsync("""
        INSERT INTO logs(timestamp,level,message,correlation_id,properties) VALUES
          (now() AT TIME ZONE 'UTC','Information','historic','historic-properties',jsonb_build_object('Password',123456,'Phone',79991234567,'DurationMs',25,'Operation','SafeRead')),
          (now() AT TIME ZONE 'UTC','Information','oversized','historic-properties',jsonb_build_object('Operation', repeat('x',70000)))
        """);
      var historic = Encoding.UTF8.GetString((await service.ExportAsync(new(from, to, "historic-properties"))).Content);
      check(!historic.Contains("123456") && !historic.Contains("79991234567") && historic.Contains("DurationMs") && historic.Contains("PropertiesTruncated"),
        "database export masks historical numeric secrets and bounds jsonb before materialization with an explicit truncation marker");
      var current = runtime.Read();
      var started = await service.StartAsync(current.Version, new(ApplicationLogLevel.Debug, 15, "Guard.Test", "PG-INC"));
      activeBuffer.Emit(TechnicalLoggingTests.Event(LogEventLevel.Debug, "Guard.Test.Operation"));
      var diagnosticBatch = activeBuffer.ReadBatch();
      check(diagnosticBatch.Single().Record.DiagnosticSessionId == started.Session!.Id, "diagnostic events retain their persistent session identity");
      await delivery.DeliverAsync(diagnosticBatch.Select(e => e.Record).ToArray(), default); activeBuffer.Acknowledge(diagnosticBatch);
      check((await service.ExportAsync(new(from, to, DiagnosticSessionId: started.Session!.Id))).Count == 1,
        "diagnostic export selects only the requested session");
      await app.Database.ExecuteSqlRawAsync("INSERT INTO logs(timestamp,level,message) SELECT now() AT TIME ZONE 'UTC','Information','limit-fixture' FROM generate_series(1,10001)");
      var limited = await service.ExportAsync(new(from, to));
      check(limited.Truncated && limited.Count == 10000 && limited.Content.Length <= 8 * 1024 * 1024, "export enforces hard row and byte limits instead of unbounded accumulation");
      await app.Database.ExecuteSqlRawAsync("INSERT INTO logs(timestamp,level,message) VALUES ((now() AT TIME ZONE 'UTC') - interval '31 days','Information','expired'), ((now() AT TIME ZONE 'UTC') - interval '29 days','Information','keep')");
      await delivery.CleanupAsync(DateTime.UtcNow.AddDays(-30), default);
      check(!await logs.Logs.AnyAsync(e => e.Message == "expired") && await logs.Logs.AnyAsync(e => e.Message == "keep"),
        "technical retention respects its own 30-day boundary without reading audit configuration");
      await TechnicalLogHttpTests.RunAsync(service, audit, check);
    } finally { await app.Database.ExecuteSqlRawAsync("DROP TABLE logs"); Directory.Delete(root, recursive: true); }
  }
  private sealed class LogsFactory(string connection) : IDbContextFactory<LogsDbContext>
  { public LogsDbContext CreateDbContext() => new(new DbContextOptionsBuilder<LogsDbContext>().UseNpgsql(connection).Options); }
}
