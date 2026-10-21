using Guard.Core.Contexts;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Logging;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using System.Text.Json;

internal static class TechnicalLoggingTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var root = Path.Combine(Path.GetTempPath(), "guard-technical-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
    try {
      var clock = new Clock(); var path = Path.Combine(root, "settings.json");
      var runtime = new TechnicalLogRuntime(path, clock);
      check(runtime.Read().Settings == new TechnicalLogSettings() && runtime.Read().Settings.BufferMegabytes == 3072,
        "technical defaults are independent: 30 days, 3072MB, 1GB reserve, 15/60 minutes, 20 errors/5 minutes");
      check(!runtime.Accept(Event(LogEventLevel.Information, "Microsoft.EntityFrameworkCore.Database.Command")) && runtime.Accept(Event(LogEventLevel.Warning, "Microsoft.EntityFrameworkCore.Database.Command")),
        "normal profile excludes routine framework SQL traffic while preserving warnings and errors");
      foreach (var invalid in new[] { new TechnicalLogSettings { NormalLevel = ApplicationLogLevel.Fatal },
        new() { NormalLevel = ApplicationLogLevel.Debug }, new() { BufferMegabytes = 0 }, new() { RetentionDays = 0 },
        new() { MinimumFreeMegabytes = -1 }, new() { MaximumDiagnosticMinutes = 10 }, new() { ErrorThreshold = 0 } })
        await PostgresTests.ThrowsAsync<ArgumentException>(() => { invalid.Validate(); return Task.CompletedTask; }, check, "invalid technical policy is rejected");
      var session = new DiagnosticLogSession(Guid.NewGuid(), "root", ApplicationLogLevel.Debug, "Guard.Core.Services", "INC-123", clock.Now, clock.Now.AddMinutes(15));
      var initial = runtime.Read(); runtime.Change(initial.Version, initial.Settings, session);
      check(runtime.Accept(Event(LogEventLevel.Debug, "Guard.Core.Services.Example")) && !runtime.Accept(Event(LogEventLevel.Debug, "Guard.Core.ServicesOther")) &&
        !runtime.Accept(Event(LogEventLevel.Debug, "Microsoft.EntityFrameworkCore")), "diagnostic prefix includes only the category and its dot-separated descendants");
      var restarted = new TechnicalLogRuntime(path, clock);
      check(restarted.Read().Session?.Id == session.Id && restarted.Read().Session?.ExpiresAtUtc == session.ExpiresAtUtc, "restart retains the original diagnostic deadline, not another duration");
      await PostgresTests.ThrowsAsync<InvalidOperationException>(() => { runtime.Change(initial.Version, initial.Settings, null); return Task.CompletedTask; }, check, "stale settings version cannot replace current diagnostic session");
      clock.Now = clock.Now.AddMinutes(16);
      File.Delete(path); Directory.CreateDirectory(path);
      runtime.Expire();
      check(runtime.Read().Session == null && !runtime.Accept(Event(LogEventLevel.Debug, "Guard.Core.Services.Example")) && runtime.ConfigurationError != null,
        "expired diagnostic filtering stops even when persisting expiry is impossible");
      Directory.Delete(path); runtime.Expire();
      check(runtime.ConfigurationError == null, "expiry persistence recovers without restarting the application");
      using var buffer = new TechnicalLogBuffer(runtime, Path.Combine(root, "buffer"), () => long.MaxValue);
      using (var manager = new DynamicLoggerManager("unused", new LoggingLevelSwitch(), runtime, buffer)) {
        manager.ApplyConfiguration(100, LogEventLevel.Information);
        var logger = manager.Logger.ForContext("SourceContext", "Guard.Core.Services.Example").ForContext("CorrelationId", "corr-123");
        logger.ForContext("Password", "private-secret").ForContext("Response", "private-payload").Error(new InvalidOperationException("private-exception"), "Failure {Password} {Response}", "private-secret", "private-payload");
        manager.ApplyConfiguration(200, LogEventLevel.Information);
        logger.Information("After replacement");
        await Task.WhenAll(Enumerable.Range(0, 64).Select(n => Task.Run(() => logger.Information("Parallel event {Count}", n))));
      }
      var batch = buffer.ReadBatch(100);
      var error = batch.Single(item => item.Record.Level == "Error").Record;
      check(batch.Count == 66 && batch.Select(item => item.Record.Id).Distinct().Count() == 66 && buffer.Health().LostEvents == 0,
        "parallel emit and logger generation replacement preserve each event and do not dispose the shared buffer");
      check(!JsonSerializer.Serialize(error).Contains("private-") && error.CorrelationId == "corr-123" && error.Exception!.Contains("InvalidOperationException"),
        "redaction removes named secrets, arbitrary payload and exception messages while preserving correlation and exception type");
      var ids = batch.Select(item => item.Record.Id).ToArray();
      buffer.Dispose();
      using var recovered = new TechnicalLogBuffer(runtime, Path.Combine(root, "buffer"), () => long.MaxValue);
      check(recovered.ReadBatch().Select(item => item.Record.Id).SequenceEqual(ids), "buffer restart retains exact event identities and ordering");
      recovered.ProbeLocal(); recovered.Worker(); recovered.DatabaseProbe();
      check(recovered.Health().Status == "Ready", "successful local and database checks make technical logging ready");
      clock.Now = clock.Now.AddSeconds(31);
      check(recovered.Health().Status == "Emergency", "stale worker state is not reported as healthy");
      var lowDirectory = Path.Combine(root, "low-space");
      using (var low = new TechnicalLogBuffer(runtime, lowDirectory, () => 1)) {
        low.Emit(Event(LogEventLevel.Error));
        check(low.Health().LostEvents == 1 && low.Health().PendingCount == 0 && low.Health().Status == "Emergency", "disk reserve rejects a technical event without throwing or blocking the system");
      }
      using (var low = new TechnicalLogBuffer(runtime, lowDirectory, () => long.MaxValue)) check(low.Health().LostEvents == 1, "loss counter survives buffer restart when its directory is writable");
      var quotaDirectory = Path.Combine(root, "quota"); Directory.CreateDirectory(quotaDirectory);
      var quotaState = runtime.Read(); runtime.Change(quotaState.Version, quotaState.Settings with { BufferMegabytes = 16 }, null);
      var retained = Path.Combine(quotaDirectory, "old.json"); using (var file = File.Create(retained)) file.SetLength(16L * 1024 * 1024);
      using (var full = new TechnicalLogBuffer(runtime, quotaDirectory, () => long.MaxValue)) {
        full.Emit(Event(LogEventLevel.Information));
        check(full.Health().LostEvents == 1 && full.Health().PendingCount == 1 && new FileInfo(retained).Length == 16L * 1024 * 1024,
          "buffer quota drops incoming events and preserves the existing backlog");
      }
      var failedDirectory = Path.Combine(root, "recoverable"); File.WriteAllText(failedDirectory, "owned-fixture");
      using (var repair = new TechnicalLogBuffer(runtime, failedDirectory, () => long.MaxValue)) {
        repair.Emit(Event(LogEventLevel.Error)); File.Delete(failedDirectory); repair.ProbeLocal(); repair.Emit(Event(LogEventLevel.Information));
        check(repair.Health().PendingCount == 1 && repair.Health().BufferError == null, "a writable directory is recovered automatically after initial local failure");
      }
      var current = runtime.Read(); runtime.Change(current.Version, current.Settings with { ErrorThreshold = 2 }, null);
      recovered.Emit(Event(LogEventLevel.Error)); recovered.Emit(Event(LogEventLevel.Error));
      check(recovered.Health().ErrorStorm, "error-rate warning counts actual error events in a bounded time window");
      clock.Now = clock.Now.AddMinutes(6);
      check(!recovered.Health().ErrorStorm && recovered.Health().ErrorsInWindow == 0, "error warning expires after the configured rolling window");
      var broken = Path.Combine(root, "broken.json"); File.WriteAllText(broken, "{\"Settings\":null}");
      check(new TechnicalLogRuntime(broken).ConfigurationError != null, "corrupt persisted policy becomes visible and does not prevent startup");
      await AccessAsync(runtime, recovered, check);
      await ExpiryRetryAsync(root, check);
    } finally { Directory.Delete(root, recursive: true); }
  }
  private static async Task ExpiryRetryAsync(string root, Action<bool, string> check)
  {
    var clock = new Clock(); var settingsPath = Path.Combine(root, "expiry-settings.json");
    var runtime = new TechnicalLogRuntime(settingsPath, clock); var initial = runtime.Read();
    var session = new DiagnosticLogSession(Guid.NewGuid(), "root", ApplicationLogLevel.Debug, "Guard", "INC-expiry", clock.Now, clock.Now.AddMinutes(1));
    runtime.Change(initial.Version, initial.Settings, session); clock.Now = clock.Now.AddMinutes(2);
    using var buffer = new TechnicalLogBuffer(runtime, Path.Combine(root, "expiry-buffer"), () => long.MaxValue);
    var audit = new AuditCapture { Fail = true };
    using var provider = new ServiceCollection().AddSingleton<IAuditService>(audit).BuildServiceProvider();
    var delivery = new TechnicalLogDelivery("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1");
    using (var worker = new TechnicalLogWorker(runtime, buffer, delivery, provider.GetRequiredService<IServiceScopeFactory>(), new InternalLogWriter(Path.Combine(root, "expiry-internal"))))
      await worker.TickAsync(default);
    check(runtime.Read().Session == null && runtime.PendingExpirations().Single().Id == session.Id && audit.Events.Count == 0,
      "audit outage does not delay automatic diagnostic filtering and preserves its pending completion");
    var restarted = new TechnicalLogRuntime(settingsPath, clock);
    check(restarted.PendingExpirations().Single().Id == session.Id, "pending diagnostic completion survives restart with the same event identity");
    audit.Fail = false;
    using (var worker = new TechnicalLogWorker(restarted, buffer, delivery, provider.GetRequiredService<IServiceScopeFactory>(), new InternalLogWriter(Path.Combine(root, "expiry-internal")))) {
      await worker.TickAsync(default); await worker.TickAsync(default);
    }
    check(audit.Events.Count == 1 && audit.Events.Single().EventId == session.Id && restarted.PendingExpirations().Count == 0 &&
      new TechnicalLogRuntime(settingsPath, clock).PendingExpirations().Count == 0,
      "audit recovery confirms exactly one automatic completion and persists acknowledgement");
  }

  private static async Task AccessAsync(TechnicalLogRuntime runtime, TechnicalLogBuffer buffer, Action<bool, string> check)
  {
    var access = new Access(); var availability = new Available(); var audit = new AuditCapture();
    var service = new TechnicalLogService(access, availability, runtime, buffer, audit, new NoDatabase());
    var before = runtime.Read();
    access.Root = false;
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(), check, "technical state is Root-only even if a permission provider allows a code");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.StartAsync(before.Version, new(ApplicationLogLevel.Debug, 15, "Guard", "INC")), check, "ordinary user cannot enable diagnostics");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.ExportAsync(new(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow)), check, "ordinary user cannot export technical logs");
    access.Root = true; availability.Block = true;
    await PostgresTests.ThrowsAsync<AuditUnavailableException>(() => service.GetAsync(), check, "technical inspection does not bypass mandatory audit outage");
    availability.Block = false; audit.Fail = true;
    await PostgresTests.ThrowsAsync<IOException>(() => service.StartAsync(before.Version, new(ApplicationLogLevel.Debug, 15, "Guard", "INC")), check, "failed audit intent prevents diagnostic activation");
    check(runtime.Read().Version == before.Version && runtime.Read().Session == null, "failed audit leaves technical policy and mode unchanged");
    audit.Fail = false;
    var started = await service.StartAsync(before.Version, new(ApplicationLogLevel.Verbose, 15, "Guard", "INC-456"));
    check(started.Session?.ActorId == "root" && audit.Events.Last().Outcome == "success", "diagnostic start persists Root identity and mandatory result audit");
    await PostgresTests.ThrowsAsync<ArgumentException>(() => service.StartAsync(started.Version, new(ApplicationLogLevel.Debug, 61, "Guard", "INC")), check, "diagnostic command enforces maximum duration");
    var stopped = await service.StopAsync(started.Version);
    check(stopped.Session == null && !runtime.Accept(Event(LogEventLevel.Debug)), "Root can return immediately to normal logging");
    await PostgresTests.ThrowsAsync<ArgumentException>(() => service.ExportAsync(new(DateTime.UtcNow.AddDays(-8), DateTime.UtcNow)), check, "oversized export period is rejected before opening a database");
    await PostgresTests.ThrowsAsync<ArgumentException>(() => service.ExportAsync(new(DateTime.Now.AddHours(-1), DateTime.Now)), check, "export dates require explicit UTC");
  }
  public static LogEvent Event(LogEventLevel level, string category = "Guard.Example") => new(DateTimeOffset.UtcNow, level, null,
    new MessageTemplateParser().Parse("Test event"), [new("SourceContext", new ScalarValue(category))]);
  private sealed class Clock : TimeProvider { public DateTime Now = DateTime.UtcNow; public override DateTimeOffset GetUtcNow() => new(Now); }
  internal sealed class Access : IPermissionService
  {
    public bool Root = true;
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(new UserAccessSnapshot("root", Root, [Permissions.Logs.Read, Permissions.Logs.Configure]));
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string code, CancellationToken ct = default) => Task.FromResult(true);
    public Task RequireAsync(string code, CancellationToken ct = default) => Task.CompletedTask;
  }
  internal sealed class Available : IOperationAvailability
  { public bool Block; public Task RequireWorkingAsync(CancellationToken ct = default) => Block ? Task.FromException(new AuditUnavailableException("Test", "Audit unavailable")) : Task.CompletedTask; }
  internal sealed class AuditCapture : IAuditService
  {
    public bool Fail; public readonly List<AuditEventData> Events = [];
    public Action<AuditEventData>? AfterRecord;
    public Task RecordAsync(AuditEventData entry, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); if (Fail) throw new IOException(); Events.Add(entry); AfterRecord?.Invoke(entry); return Task.CompletedTask; }
    public void LogIdentityEvent(AuditEventType type, string? actor, string? ip = null, string? details = null) => throw new NotSupportedException();
  }
  private sealed class NoDatabase : IDbContextFactory<LogsDbContext>
  { public LogsDbContext CreateDbContext() => throw new InvalidOperationException("Database must not be reached"); }
}
