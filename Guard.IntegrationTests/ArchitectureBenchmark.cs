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
      // По 100 тыс. строк: сохраняем индексы/FK модели и ограничиваем размер одной транзакции.
      for (var batch = 0; batch < 5; batch++) {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
          INSERT INTO "IpAddresses" ("Id", "Address", "SubdivisionId", "Status", "InsertedDate", "CreatedBy")
          SELECT md5('ip' || i)::uuid, '2001:db8:' || to_hex(i / 65536) || '::' || to_hex(i % 65536),
            s."Id", 1, TIMESTAMPTZ '2026-10-16 00:00:00+00', 'benchmark'
          FROM generate_series({batch * 100000L + 1}, {(batch + 1) * 100000L}) i
          JOIN bench_subs s ON s.n = (i - 1) % 1000 + 1;
          """, ct);
        Console.WriteLine($"Seed IP: {(batch + 1) * 100000}; elapsed={seed.Elapsed.TotalSeconds:F1}s");
      }
      await db.Database.ExecuteSqlRawAsync("ANALYZE", ct);
    }
    finally { await db.Database.CloseConnectionAsync(); }
    check(await db.Personals.CountAsync(ct) == 200000, "benchmark employees count");
    check(await db.IpAddresses.CountAsync(ct) == 500000, "benchmark IP count");
    check(await db.Subdivisions.CountAsync(ct) == 1000, "benchmark subdivisions count");
    var preparation = Stopwatch.StartNew();
    var maintenance = new IpCatalogKeyMaintenance(factory);
    long prepared = 0;
    while (true) {
      var batch = await maintenance.PrepareBatchAsync(ct: ct);
      prepared += batch.Updated;
      check(batch.Invalid == 0, "benchmark generated IP keys are valid");
      if (batch.Updated == 0) break;
      if (prepared % 100000 == 0)
        Console.WriteLine($"Prepared IP keys: {prepared}; elapsed={preparation.Elapsed.TotalSeconds:F1}s");
    }
    check(prepared == 500000, "benchmark prepares all five hundred thousand legacy IP keys");
    Console.WriteLine($"IP key preparation 500000: {preparation.Elapsed.TotalSeconds:F1}s");
    // Backfill updates every row: refresh statistics and remove dead index entries before steady-state measurements.
    await db.Database.ExecuteSqlRawAsync("VACUUM (ANALYZE) \"IpAddresses\"", ct);
    Console.WriteLine($"Database bytes: {await db.Database.SqlQueryRaw<long>("SELECT pg_database_size(current_database()) AS \"Value\"").SingleAsync(ct)}");

    var root = new UserAccessSnapshot("benchmark", true, []);
    db.Users.Add(new ApplicationUser { Id = root.UserId, UserName = "benchmark",
      PersonalId = await db.Personals.OrderBy(p => p.PersonalId).Select(p => p.Id).FirstAsync(ct) });
    await db.SaveChangesAsync(ct);
    var ipSearch = new IpAddressService(factory, new BenchmarkAccess(root.UserId), NullLogger<IpAddressService>.Instance);
    await MeasureAsync("IP indexed create and cleanup on 500000", async () => {
      var id = await ipSearch.CreateAsync(new("198.18.240.1", null, null), ct);
      check(await db.IpAddresses.AnyAsync(ip => ip.Id == id, ct), "benchmark indexed create stores new IP");
      await db.IpAddresses.Where(ip => ip.Id == id).ExecuteDeleteAsync(ct);
    });
    await MeasureAsync("IP indexed equivalent duplicate on 500000", async () => {
      await PostgresTests.ThrowsAsync<ArgumentException>(() => ipSearch.CreateAsync(new("2001:db8::1/128", null, null), ct),
        check, "benchmark indexed check rejects equivalent legacy IPv6 host");
    });
    var rootScope = new DataAccessScope(root.UserId, true, []);
    // Only the runner's disposable database: compare the same data and SQL with/without the new index.
    await db.Database.ExecuteSqlRawAsync("DROP INDEX \"IX_Personals_ActiveName\"", ct);
    await MeasurePersonalQueriesAsync("before index", factory, root, rootScope, check, ct);
    await db.Database.ExecuteSqlRawAsync("""
      CREATE INDEX "IX_Personals_ActiveName" ON "Personals" ("LastName", "FirstName", "Id")
      WHERE "Status" IN (1, 2, 3);
      ANALYZE "Personals";
      """, ct);
    await MeasurePersonalQueriesAsync("after index", factory, root, rootScope, check, ct);
    var personalAccess = new PersonalAccess(root, rootScope);
    var personalSearch = new PersonalService(new ReadRepository<Personal>(factory, personalAccess, personalAccess), null!,
      NullLogger<PersonalService>.Instance, personalAccess, personalAccess, factory);
    foreach (var skip in new[] { 0, 100000 }) {
      await MeasureAsync($"personals SearchAsync root skip={skip}", async () => {
        var result = await personalSearch.SearchAsync(new(AllSubdivisions: true, Skip: skip), ct);
        check(result.Count == 200000 && result.Items.Count == 20, "benchmark personal service page");
      });
    }
    foreach (var ownOnly in new[] { false, true }) {
      await MeasureAsync($"IP count+page ownOnly={ownOnly}", async () => {
        var result = await ipSearch.SearchAsync(new(AllSubdivisions: !ownOnly, OrderBy: "Address asc"), ct);
        check(result.Count == (ownOnly ? 500 : 500000) && result.Items.Count == 20, "benchmark IP search page");
      });
    }
    await MeasureAsync("IP SearchAsync default sort all", async () => {
      var result = await ipSearch.SearchAsync(new(AllSubdivisions: true), ct);
      check(result.Count == 500000 && result.Items.Count == 20, "benchmark IP default sort page");
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

  private static async Task MeasurePersonalQueriesAsync(string label, IDbContextFactory<ApplicationDbContext> factory,
    UserAccessSnapshot root, DataAccessScope scope, Action<bool, string> check, CancellationToken ct)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    var query = await ExportQueries.PersonalsAsync(db, new(AllSubdivisions: true), root, scope, ct);
    await MeasureAsync($"personals {label} count only", async () =>
      check(await query.CountAsync(ct) == 200000, "benchmark isolated employee count"));
    foreach (var skip in new[] { 0, 100000 }) {
      var page = query.Skip(skip).Take(20).Select(p => new PersonalListItemDto(p.Id, p.LastName, p.FirstName,
        p.MiddleName, p.FullName, p.Subdivision == null ? null : p.Subdivision.Name, p.Status, p.Version));
      await MeasureAsync($"personals {label} page only skip={skip}", async () =>
        check((await page.ToListAsync(ct)).Count == 20, "benchmark isolated employee page"));
      await using var command = page.CreateDbCommand();
      command.CommandText = "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + command.CommandText;
      await db.Database.OpenConnectionAsync(ct);
      try {
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        Console.WriteLine($"Personal plan {label} skip={skip}: {reader.GetString(0)}");
      }
      finally { await db.Database.CloseConnectionAsync(); }
    }
  }

  private sealed class BenchmarkAccess(string userId) : IIpManagementAccessService
  {
    public Task<IpManagementScope> GetScopeAsync(bool write, CancellationToken ct = default) =>
      Task.FromResult(new IpManagementScope(userId, true, []));
  }

  private sealed class PersonalAccess(UserAccessSnapshot user, DataAccessScope scope) : IPermissionService, IDataAccessScopeService
  {
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(user);
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => Task.FromResult(user);
    public Task<bool> HasAsync(string permission, CancellationToken ct = default) => Task.FromResult(user.Has(permission));
    public Task RequireAsync(string permission, CancellationToken ct = default)
    {
      ct.ThrowIfCancellationRequested();
      if (!user.Has(permission)) throw new UnauthorizedAccessException();
      return Task.CompletedTask;
    }
    public Task<DataAccessScope> GetAsync(CancellationToken ct = default) => Task.FromResult(scope);
  }
}
