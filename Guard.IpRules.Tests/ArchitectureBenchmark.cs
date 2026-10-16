using System.Diagnostics;
using System.Globalization;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Guard.Core.Services.Exports;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

// Отдельный, явно включаемый замер. Не входит в обычные проверки.
internal static class ArchitectureBenchmark
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(30));
    var ct = deadline.Token;
    await using var db = await factory.CreateDbContextAsync(ct);
    db.Database.SetCommandTimeout(1200);
    Console.WriteLine($"Benchmark runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; CPU={Environment.ProcessorCount}");
    var subdivisions = Enumerable.Range(1, 1000).Select(i => new Subdivision {
      Name = $"Synthetic {i:D4}", Path = $"/{i}/", CreatedBy = "benchmark", InsertedDate = DateTime.UtcNow
    }).ToArray();
    db.Subdivisions.AddRange(subdivisions);
    await db.SaveChangesAsync(ct);
    // PostgreSQL назначает identity в порядке фактических INSERT, не порядка массива.
    foreach (var subdivision in subdivisions) subdivision.Path = $"/{subdivision.SubdivisionId}/";
    await db.SaveChangesAsync(ct);
    var hierarchy = new ExportHierarchy(subdivisions.Select(s => new ExportSubdivision(s.Id, s.ParentId, s.SubdivisionId, s.Name, s.Path)));
    foreach (var subdivision in subdivisions) check(hierarchy.Get(subdivision.Id).Length == 1, "benchmark hierarchy valid before bulk seed");
    // TEMP TABLE живёт на открытом соединении, удерживаем его при подготовке.
    await db.Database.OpenConnectionAsync(ct);
    try {
      await db.Database.ExecuteSqlRawAsync("""
        CREATE TEMP TABLE IF NOT EXISTS bench_subs AS
          SELECT "Id", row_number() OVER (ORDER BY "Name") AS n FROM "Subdivisions";
        """, ct);
      var seed = Stopwatch.StartNew();
      await db.Database.ExecuteSqlRawAsync("""
        INSERT INTO "Personals" ("Id", "LastName", "FirstName", "SubdivisionId", "PersonalSubdivisionId",
          "ImportedFromApi", "ArchivedBySynchronization", "Version", "Status", "InsertedDate", "CreatedBy")
        SELECT md5('personal' || i)::uuid, CASE WHEN i <= 100000 THEN 'Export' ELSE 'Other' END,
          'Synthetic ' || lpad(i::text, 6, '0'), s."Id", s.n, false, false,
          md5('version' || i)::uuid, 1, TIMESTAMPTZ '2026-10-16 00:00:00+00', 'benchmark'
        FROM generate_series(1, 200000) i JOIN bench_subs s ON s.n = (i - 1) % 1000 + 1;
        """, ct);
      // По 1 млн строк: сохраняем индексы/FK модели и ограничиваем размер одной транзакции.
      for (var batch = 0; batch < 10; batch++) {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
          INSERT INTO "IpAddresses" ("Id", "Address", "SubdivisionId", "Status", "InsertedDate", "CreatedBy")
          SELECT md5('ip' || i)::uuid, '2001:db8:' || to_hex(i / 65536) || '::' || to_hex(i % 65536),
            s."Id", 1, TIMESTAMPTZ '2026-10-16 00:00:00+00', 'benchmark'
          FROM generate_series({batch * 1000000L + 1}, {(batch + 1) * 1000000L}) i
          JOIN bench_subs s ON s.n = (i - 1) % 1000 + 1;
          """, ct);
        Console.WriteLine($"Seed IP: {(batch + 1) * 1000000}; elapsed={seed.Elapsed.TotalSeconds:F1}s");
      }
      await db.Database.ExecuteSqlRawAsync("ANALYZE", ct);
    }
    finally { await db.Database.CloseConnectionAsync(); }
    check(await db.Personals.CountAsync(ct) == 200000, "benchmark employees count");
    check(await db.IpAddresses.CountAsync(ct) == 10000000, "benchmark IP count");
    check(await db.Subdivisions.CountAsync(ct) == 1000, "benchmark subdivisions count");
    Console.WriteLine($"Database bytes: {await db.Database.SqlQueryRaw<long>("SELECT pg_database_size(current_database()) AS \"Value\"").SingleAsync(ct)}");

    var root = new UserAccessSnapshot("benchmark", true, []);
    db.Users.Add(new ApplicationUser { Id = root.UserId, UserName = "benchmark",
      PersonalId = await db.Personals.OrderBy(p => p.PersonalId).Select(p => p.Id).FirstAsync(ct) });
    await db.SaveChangesAsync(ct);
    var ipSearch = new IpAddressService(factory, new BenchmarkAccess(root.UserId), NullLogger<IpAddressService>.Instance);
    var rootScope = new DataAccessScope(root.UserId, true, []);
    foreach (var skip in new[] { 0, 100000 }) {
      await MeasureAsync($"personals root skip={skip}", async () => {
        await using var queryDb = await factory.CreateDbContextAsync(ct);
        var q = await ExportQueries.PersonalsAsync(queryDb, new(AllSubdivisions: true), root, rootScope, ct);
        var count = await q.CountAsync(ct);
        var rows = await q.Skip(skip).Take(20).Select(p => new PersonalListItemDto(p.Id, p.LastName, p.FirstName,
          p.MiddleName, p.FullName, p.Subdivision == null ? null : p.Subdivision.Name, p.Status, p.Version)).ToListAsync(ct);
        check(count == 200000 && rows.Count == 20, "benchmark personal page");
      });
    }
    foreach (var ownOnly in new[] { false, true }) {
      await MeasureAsync($"IP count+page ownOnly={ownOnly}", async () => {
        var result = await ipSearch.SearchAsync(new(AllSubdivisions: !ownOnly, OrderBy: "Address asc"), ct);
        check(result.Count == (ownOnly ? 10000 : 10000000) && result.Items.Count == 20, "benchmark IP search page");
      });
    }
    await MeasureAsync("IP SearchAsync default sort all", async () => {
      var result = await ipSearch.SearchAsync(new(AllSubdivisions: true), ct);
      check(result.Count == 10000000 && result.Items.Count == 20, "benchmark IP default sort page");
    });

    var tempRoot = Path.Combine(Path.GetTempPath(), "guard-benchmark-" + Guid.NewGuid().ToString("N"));
    var options = Options.Create(new ExportOptions { Directory = "exports", RowsPerPart = 50000 });
    var storage = new ExportStorage(new EphemeralDataProtectionProvider(), options, new ExportTests.EnvironmentStub(tempRoot));
    var job = new ExportJob { AuthorId = root.UserId, AuthorName = "Synthetic", Area = ExportArea.Personals, Format = ExportFormat.Excel };
    try {
      var request = new ExportRequest(ExportArea.Personals, ExportFormat.Excel, AllSubdivisions: true,
        PersonalFilter: new("LastName", "Equals", "Export"));
      var timer = Stopwatch.StartNew();
      var snapshot = await new ExportSnapshot(factory, storage).CaptureAsync(job, request,
        new(root, null, null, [], "benchmark"), _ => Task.CompletedTask, ct);
      check(snapshot.Count == 100000, "benchmark export exactly 100000 rows");
      Console.WriteLine($"Export snapshot 100000: {timer.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture)} ms");
      timer.Restart();
      var file = await new ExportDocumentWriter(storage, options).WriteAsync(job, request, snapshot,
        new(job.Id, "Synthetic benchmark", "Synthetic", job.CreatedAtUtc, "UTC", "Synthetic filter"), _ => Task.CompletedTask, ct);
      Console.WriteLine($"Export XLSX/ZIP: {timer.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture)} ms; bytes={new FileInfo(file.Path).Length}");
      check(File.Exists(file.Path), "benchmark export file exists");
    }
    finally {
      // Только собственный случайный каталог; никаких рабочих архивов.
      if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
    }
    Console.WriteLine("Benchmark completed; temporary database cleanup follows.");
  }

  private static async Task MeasureAsync(string name, Func<Task> action)
  {
    // Первый вызов отдельно, затем пять последовательных прогретых замеров.
    var samples = new List<double>();
    for (var i = 0; i < 6; i++) {
      var timer = Stopwatch.StartNew(); await action(); samples.Add(timer.Elapsed.TotalMilliseconds);
    }
    var warm = samples.Skip(1).Order().ToArray();
    Console.WriteLine($"{name}: first={samples[0].ToString("F1", CultureInfo.InvariantCulture)} ms; warm median={warm[2].ToString("F1", CultureInfo.InvariantCulture)} ms; warm max={warm[^1].ToString("F1", CultureInfo.InvariantCulture)} ms");
  }

  private sealed class BenchmarkAccess(string userId) : IIpManagementAccessService
  {
    public Task<IpManagementScope> GetScopeAsync(bool write, CancellationToken ct = default) =>
      Task.FromResult(new IpManagementScope(userId, true, []));
  }
}
