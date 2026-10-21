using Guard.Core.Contexts;
using Guard.Core.Logging;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;
using Serilog.Parsing;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

// Явный нагрузочный режим. Измеряет только логирование, не имитирует полноценные 1000 Blazor circuits.
internal static class TechnicalLoggingBenchmark
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    await using var db = await factory.CreateDbContextAsync();
    if (!db.Database.GetDbConnection().Database.StartsWith("guard_ip_test_", StringComparison.Ordinal)) throw new InvalidOperationException("Isolated database required");
    var connection = db.Database.GetConnectionString()!;
    DatabaseInitializer.EnsureLogsDatabaseExists(connection);
    var directory = Path.Combine(Path.GetTempPath(), "guard-log-benchmark-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try {
      Console.WriteLine($"Technical benchmark: CPU={Environment.ProcessorCount}; .NET={Environment.Version}; clients=1000; operations/client=3");
      foreach (var diagnostic in new[] { false, true }) {
        var runtime = new TechnicalLogRuntime(Path.Combine(directory, $"{diagnostic}-settings.json"));
        if (diagnostic) { var current = runtime.Read(); runtime.Change(current.Version, current.Settings,
          new(Guid.NewGuid(), "root", ApplicationLogLevel.Debug, "Guard.Benchmark", "SYNTHETIC", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15))); }
        using var buffer = new TechnicalLogBuffer(runtime, Path.Combine(directory, diagnostic ? "diagnostic" : "normal"), () => long.MaxValue);
        using var process = Process.GetCurrentProcess();
        var cpuBefore = process.TotalProcessorTime; var allocatedBefore = GC.GetTotalAllocatedBytes();
        var durations = new ConcurrentBag<double>(); var totals = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, 1000).Select(async client => {
          await Task.Yield();
          for (var operation = 0; operation < 3; operation++) {
            var elapsed = Stopwatch.StartNew();
            var failure = client % 100 == 0 ? new InvalidOperationException("never-persist-this-message", new TimeoutException("private-endpoint")) : null;
            buffer.Emit(new(DateTimeOffset.UtcNow, failure == null ? LogEventLevel.Information : LogEventLevel.Error, failure,
              new MessageTemplateParser().Parse("Operation {Operation} {EntityId} completed in {DurationMs} ms"),
              [new("Operation", new ScalarValue("SyntheticRead")), new("EntityId", new ScalarValue(client)), new("DurationMs", new ScalarValue(25)),
               new("SourceContext", new ScalarValue("Guard.Benchmark.Read")), new("CorrelationId", new ScalarValue($"synthetic-{client}-{operation}"))]));
            for (var stage = 0; stage < 4; stage++) buffer.Emit(new(DateTimeOffset.UtcNow, LogEventLevel.Debug, null,
              new MessageTemplateParser().Parse("Stage {Stage} took {DurationMs} ms"),
              [new("Stage", new ScalarValue("SyntheticStage")), new("DurationMs", new ScalarValue(stage)), new("SourceContext", new ScalarValue("Guard.Benchmark.Read"))]));
            durations.Add(elapsed.Elapsed.TotalMilliseconds);
          }
        }));
        totals.Stop();
        process.Refresh();
        var cpuSeconds = (process.TotalProcessorTime - cpuBefore).TotalSeconds;
        var workingSet = process.WorkingSet64; var managedMemory = GC.GetTotalMemory(false);
        var before = buffer.Health(); var expected = diagnostic ? 15000 : 3000;
        check(before.PendingCount == expected && before.LostEvents == 0, "benchmark producers persist every accepted synthetic event");
        var sorted = durations.Order().ToArray();
        var rate = before.PendingCount / totals.Elapsed.TotalSeconds;
        var averageBytes = (double)before.PendingBytes / before.PendingCount;
        var inspection = Stopwatch.StartNew();
        var preview = buffer.Snapshot(new(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1), Source: TechnicalLogSource.LocalBuffer), default);
        inspection.Stop();
        check(preview.Records.Count == 1000 && preview.Truncated && buffer.Health().PendingCount == expected,
          "benchmark local inspection is bounded and does not acknowledge queued events");
        var delivery = new TechnicalLogDelivery(connection); var drain = Stopwatch.StartNew(); var delivered = 0;
        while (true) {
          var batch = buffer.ReadBatch(500); if (batch.Count == 0) break;
          await delivery.DeliverAsync(batch.Select(e => e.Record).ToArray(), default); buffer.Acknowledge(batch); delivered += batch.Count;
        }
        drain.Stop();
        check(delivered == expected && await db.Database.ExecuteSqlRawAsync("DELETE FROM logs") == expected, "benchmark recovery delivers the exact queued count without duplicates");
        var seconds12h = TimeSpan.FromHours(12).TotalSeconds;
        // Плановый профиль: одна операция в минуту на пользователя, два штатных события / десять диагностических.
        var plannedEventsPerSecond = 1000d / 60 * (diagnostic ? 10 : 2);
        Console.WriteLine(JsonSerializer.Serialize(new { Profile = diagnostic ? "diagnostic" : "normal", Clients = 1000,
          Events = expected, EmitSeconds = totals.Elapsed.TotalSeconds, EventsPerSecond = rate, AverageBytes = averageBytes,
          EmitCpuSeconds = cpuSeconds, EmitCpuPercentOfMachine = cpuSeconds / totals.Elapsed.TotalSeconds / Environment.ProcessorCount * 100,
          WorkingSetMiB = workingSet / 1048576d, ManagedMemoryMiB = managedMemory / 1048576d,
          AllocatedMiB = (GC.GetTotalAllocatedBytes() - allocatedBefore) / 1048576d,
          LogicalWriteMiBPerSecond = before.PendingBytes / totals.Elapsed.TotalSeconds / 1048576d, LocalPreviewMs = inspection.Elapsed.TotalMilliseconds,
          OperationP50Ms = sorted[sorted.Length / 2], OperationP95Ms = sorted[(int)(sorted.Length * .95)], OperationP99Ms = sorted[(int)(sorted.Length * .99)],
          DrainSeconds = drain.Elapsed.TotalSeconds, RawDeliveryEventsPerSecond = expected / drain.Elapsed.TotalSeconds,
          WorkerEventsPerSecond = expected / (drain.Elapsed.TotalSeconds + Math.Ceiling(expected / 500d) * 2),
          Capacity512MiBHours = Math.Min(512L * 1024 * 1024 / averageBytes, TechnicalLogBuffer.MaximumFiles) / plannedEventsPerSecond / 3600,
          Required12hMiB = Math.Ceiling(plannedEventsPerSecond * seconds12h * averageBytes / 1024 / 1024 * 1.3),
          Required12hFiles = plannedEventsPerSecond * seconds12h, Assumption = "one operation/minute/user; 2 normal or 10 diagnostic events/operation; +30% byte reserve" }));
      }
    } finally { await db.Database.ExecuteSqlRawAsync("DROP TABLE logs"); Directory.Delete(directory, true); }
  }
}
