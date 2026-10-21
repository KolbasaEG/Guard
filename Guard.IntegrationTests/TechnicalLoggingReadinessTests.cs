using Guard.Core.Contexts;
using Guard.Core.Logging;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Serilog.Events;
using Serilog.Parsing;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

internal static class TechnicalLoggingReadinessTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var directory = Path.Combine(Path.GetTempPath(), "guard-readiness-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try {
      var clock = new Clock(); var runtime = new TechnicalLogRuntime(Path.Combine(directory, "settings.json"), clock);
      var previous = runtime.Read(); runtime.Change(previous.Version, previous.Settings with { BufferMegabytes = 512 }, null);
      check(new TechnicalLogRuntime(Path.Combine(directory, "settings.json"), clock).Read().Settings.BufferMegabytes == 512,
        "new 3072MiB default never overwrites an existing explicit 512MiB policy");
      var pg = new PostgresException("private-SQL-value", "ERROR", "ERROR", "23505");
      var graph = new AggregateException("private-aggregate", new InvalidOperationException("private-outer", new IOException("private-inner", pg)),
        new HttpRequestException("private-http", null, System.Net.HttpStatusCode.Unauthorized));
      var record = TechnicalLogRecord.Create(new(DateTimeOffset.UtcNow, LogEventLevel.Error, graph,
        new MessageTemplateParser().Parse("Read {EntityType}"), [new("EntityType", new ScalarValue("Personal"))]), runtime);
      check(record.Exception!.Contains("Npgsql.PostgresException") && record.Exception.Contains("SQLSTATE:23505") && record.Exception.Contains("HTTP:401") &&
        !record.Exception.Contains("private-"), "nested and aggregate exceptions retain types and technical codes without arbitrary messages");
      var exported = TechnicalException.ForExport(record.Exception)!;
      check(exported.Contains("SQLSTATE:23505") && exported.Contains("Parent=3") && !exported.Contains("private-"), "diagnostic export retains safe exception graph relationships and root codes");
      Exception deep = new IOException("private-root");
      for (var index = 0; index < 30; index++) deep = new InvalidOperationException("private-" + index, deep);
      var bounded = TechnicalException.Describe(deep);
      check(bounded.Length <= TechnicalException.MaximumLength + 20 && bounded.Contains("[ExceptionChainTruncated]") && bounded.Split("Exception[").Length == 9,
        "deep exception chains have explicit node and size bounds with a truncation marker");
      check(!TechnicalException.ForExport("old error password=private-secret\nat Legacy.Method()\nprivate-payload")!.Contains("private-"), "historical exception messages are not reintroduced by export");
      var props = TechnicalLogPrivacy.ExportProperties("{\"Operation\":\"Load\",\"Password\":123456,\"Phone\":79991234567,\"Token\":true,\"Response\":{},\"DurationMs\":12,\"EntityType\":\"Personal\"}");
      check(props.Count == 3 && (string?)props["Operation"] == "Load" && (decimal)props["DurationMs"]! == 12 && !JsonSerializer.Serialize(props).Contains("private-"),
        "export preserves useful allowlisted structured fields while excluding payloads and secrets");
      using var buffer = new TechnicalLogBuffer(runtime, Path.Combine(directory, "buffer"), () => long.MaxValue);
      buffer.Emit(TechnicalLoggingTests.Event(LogEventLevel.Information)); buffer.Emit(TechnicalLoggingTests.Event(LogEventLevel.Error));
      var before = buffer.ReadBatch();
      var audit = new TechnicalLoggingTests.AuditCapture(); var access = new TechnicalLoggingTests.Access(); var availability = new TechnicalLoggingTests.Available();
      var service = new TechnicalLogService(access, availability, runtime, buffer, audit, new Unreachable());
      var request = new TechnicalLogExportRequest(clock.Now.AddHours(-1), clock.Now.AddHours(1), Source: TechnicalLogSource.LocalBuffer, Level: "Error");
      var file = await service.ExportAsync(request); var text = Encoding.UTF8.GetString(file.Content);
      using var manifest = JsonDocument.Parse(text.Split('\n')[0]);
      check(file.Count == 1 && manifest.RootElement.GetProperty("Source").GetString() == "LocalBuffer" && !file.Truncated &&
        text.Contains("MessageTemplate") && buffer.ReadBatch().Select(e => e.Record.Id).SequenceEqual(before.Select(e => e.Record.Id)),
        "Root can inspect/export filtered local pending records with logs DB unreachable and no queue mutation");
      check(audit.Events.First().EventType == Guard.Core.Enums.AuditEventType.ExportRequested && audit.Events.Last().Outcome == "success", "local diagnostic packages require successful intent and result audit");
      access.Root = false;
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.ExportAsync(request), check, "ordinary users cannot read local buffer by changing source");
      access.Root = true; availability.Block = true;
      await PostgresTests.ThrowsAsync<AuditUnavailableException>(() => service.ExportAsync(request), check, "local buffer export cannot bypass mandatory audit outage");
      availability.Block = false; audit.Fail = true;
      await PostgresTests.ThrowsAsync<IOException>(() => service.ExportAsync(request), check, "failed mandatory audit prevents local diagnostic package");
      audit.Fail = false;
      audit.AfterRecord = entry => { if (entry.EventType == Guard.Core.Enums.AuditEventType.ExportArchived) access.Root = false; };
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.ExportAsync(request), check, "Root revoked during final export audit cannot receive even a local preview package");
      audit.AfterRecord = null; access.Root = true;
      audit.AfterRecord = entry => { if (entry.EventType == Guard.Core.Enums.AuditEventType.ExportArchived) availability.Block = true; };
      await PostgresTests.ThrowsAsync<AuditUnavailableException>(() => service.ExportAsync(request), check, "audit availability is checked again after final package audit");
      audit.AfterRecord = null; availability.Block = false;
      await PostgresTests.ThrowsAsync<ArgumentException>(() => service.ExportAsync(request with { Source = (TechnicalLogSource)123 }), check, "unknown export source is rejected");
      await PostgresTests.ThrowsAsync<ArgumentException>(() => service.ExportAsync(request with { Level = "../Error" }), check, "unknown export level is rejected");
      using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => { buffer.Snapshot(request, cancelled.Token); return Task.CompletedTask; }, check, "local snapshot observes cancellation without removing queued records");
      buffer.ProbeLocal(); buffer.Worker(); buffer.DatabaseProbe();
      check(buffer.Health().Status == "Ready", "all fresh probes make technical logging ready");
      buffer.Worker("ConnectionUnavailable"); var fault = buffer.Health();
      check(fault.Status == "Degraded" && fault.FailureSinceUtc == clock.Now && fault.Problems!.Contains("Database:ConnectionUnavailable"), "current database failure carries explicit reason and observation time");
      clock.Now = clock.Now.AddSeconds(1); buffer.Worker(); buffer.DatabaseProbe();
      check(buffer.Health().Status == "Ready" && buffer.Health().FailureSinceUtc == null && buffer.Health().LastRecoveryUtc == clock.Now, "health automatically returns to ready and records recovery");
      clock.Now = clock.Now.AddSeconds(61); buffer.ProbeLocal(); buffer.Worker();
      check(buffer.Health().Status == "Degraded" && buffer.Health().Problems!.Contains("DatabaseProbeStale"), "live worker without a recent INSERT probe is not silently healthy");
      using var second = new TechnicalLogBuffer(runtime, buffer.DirectoryPath, () => long.MaxValue);
      second.Emit(TechnicalLoggingTests.Event(LogEventLevel.Information));
      check(second.Health().LostEvents == 1 && buffer.ReadBatch().Count == 2, "a second directory owner fails open without modifying the first queue");
      var lowSpace = false;
      using var recovering = new TechnicalLogBuffer(runtime, Path.Combine(directory, "recover-loss"), () => lowSpace ? 1 : long.MaxValue);
      lowSpace = true; recovering.Emit(TechnicalLoggingTests.Event(LogEventLevel.Information));
      lowSpace = false; recovering.ProbeLocal(); recovering.Worker(); recovering.DatabaseProbe();
      check(recovering.Health().LostEvents == 1 && recovering.Health().Status == "Ready", "historical losses remain visible separately from current recovered availability");
      var capture = new Capture<object>(); var failure = new IOException("private-failure");
      capture.LogOperationFailure(failure, "Save failed"); capture.LogOperationFailure(failure, "Commit failed"); capture.LogOperationFailure(failure, "UI failed");
      capture.LogOperationFailure(new EntityRuleException("expected"), "Validation");
      using var stopping = new CancellationTokenSource(); stopping.Cancel();
      capture.LogOperationFailure(new OperationCanceledException(stopping.Token), stopping.Token, "Cancelled");
      capture.LogOperationFailure(new OperationCanceledException(stopping.Token), "Timeout while caller still active");
      check(capture.Levels.Count(e => e == LogLevel.Error) == 2 && capture.Levels.Count(e => e == LogLevel.Warning) == 1 && capture.Levels.Count(e => e == LogLevel.Debug) == 3,
        "propagated exception produces one Error, validation is Warning, confirmed cancellation is Debug and timeout remains Error");
      for (var index = 0; index < 1003; index++) buffer.Emit(TechnicalLoggingTests.Event(LogEventLevel.Information));
      var page1 = buffer.Snapshot(request with { Level = null }, default);
      check(page1.Truncated && page1.Records.Count == 1000 && page1.NextCursor?.Length == 52, "bounded local snapshot provides an explicit continuation cursor");
      var page2 = buffer.Snapshot(request with { Level = null, AfterCursor = page1.NextCursor }, default);
      check(!page2.Truncated && page2.Records.Count == 5 && !page2.Records.Select(e => e.Record.Id).Intersect(page1.Records.Select(e => e.Record.Id)).Any(),
        "continuation covers the remainder of pending records without duplicate identities");
      await PostgresTests.ThrowsAsync<ArgumentException>(() => service.ExportAsync(request with { AfterCursor = "../../settings.json" }), check, "local continuation cannot inject a server file path");
      await CrashAndRestoreAsync(directory, check);
    } finally { Directory.Delete(directory, true); }
  }
  public static async Task CrashChildAsync(string directory)
  {
    var runtime = new TechnicalLogRuntime(Path.Combine(directory, "settings.json"));
    var state = runtime.Read(); runtime.Change(state.Version, state.Settings,
      new(Guid.NewGuid(), "root", ApplicationLogLevel.Debug, "Guard", "SYNTHETIC-CRASH", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15)));
    using var buffer = new TechnicalLogBuffer(runtime, Path.Combine(directory, "buffer"), () => long.MaxValue);
    buffer.Emit(TechnicalLoggingTests.Event(LogEventLevel.Information)); buffer.Emit(TechnicalLoggingTests.Event(LogEventLevel.Error));
    // Ещё не принятая запись: процесс прерывается между записью временного файла и rename.
    using var partial = new FileStream(Path.Combine(buffer.DirectoryPath, "incomplete.json.tmp"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
    partial.Write(Encoding.UTF8.GetBytes("{\"partial\":")); partial.Flush(true);
    Console.WriteLine(JsonSerializer.Serialize(buffer.ReadBatch().Select(e => e.Record.Id).ToArray()));
    await Console.Out.FlushAsync(); await Task.Delay(Timeout.Infinite);
  }
  private static async Task CrashAndRestoreAsync(string parent, Action<bool, string> check)
  {
    var directory = Path.Combine(parent, "crash-child"); Directory.CreateDirectory(directory);
    var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = directory };
    start.ArgumentList.Add(typeof(TechnicalLoggingReadinessTests).Assembly.Location);
    start.ArgumentList.Add("--technical-crash-probe"); start.ArgumentList.Add(directory);
    using var child = Process.Start(start) ?? throw new InvalidOperationException("Test child not started");
    try {
      var line = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(20));
      var ids = JsonSerializer.Deserialize<Guid[]>(line ?? "")!;
      child.Kill(entireProcessTree: true); await child.WaitForExitAsync();
      var runtime = new TechnicalLogRuntime(Path.Combine(directory, "settings.json"));
      using (var restored = new TechnicalLogBuffer(runtime, Path.Combine(directory, "buffer"), () => long.MaxValue)) {
        check(restored.ReadBatch().Select(e => e.Record.Id).SequenceEqual(ids) && restored.Health().LostEvents == 0 &&
          !File.Exists(Path.Combine(restored.DirectoryPath, "incomplete.json.tmp")), "forced process termination preserves all accepted durable event identities and discards only an unaccepted partial file");
        check(runtime.Read().Session != null, "forced termination preserves the original persisted diagnostic deadline");
      }
      var copy = Path.Combine(parent, "restore-copy"); Directory.CreateDirectory(copy);
      File.Copy(Path.Combine(directory, "settings.json"), Path.Combine(copy, "settings.json"));
      Directory.CreateDirectory(Path.Combine(copy, "buffer"));
      foreach (var file in Directory.EnumerateFiles(Path.Combine(directory, "buffer"), "*.json")) File.Copy(file, Path.Combine(copy, "buffer", Path.GetFileName(file)));
      var copyRuntime = new TechnicalLogRuntime(Path.Combine(copy, "settings.json"));
      using var copyBuffer = new TechnicalLogBuffer(copyRuntime, Path.Combine(copy, "buffer"), () => long.MaxValue);
      check(copyBuffer.ReadBatch().Select(e => e.Record.Id).SequenceEqual(ids) && copyRuntime.Read().Session?.ExpiresAtUtc == runtime.Read().Session!.ExpiresAtUtc,
        "offline backup restored to another directory retains event identities and never extends diagnostic time");
    } finally { if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); } }
  }
  private sealed class Clock : TimeProvider { public DateTime Now = DateTime.UtcNow; public override DateTimeOffset GetUtcNow() => new(Now); }
  private sealed class Unreachable : IDbContextFactory<LogsDbContext>
  { public LogsDbContext CreateDbContext() => throw new Exception("Local export must not query logs database"); }
  internal sealed class Capture<T> : ILogger<T>
  {
    public readonly List<LogLevel> Levels = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel level) => true;
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Levels.Add(level);
  }
}
