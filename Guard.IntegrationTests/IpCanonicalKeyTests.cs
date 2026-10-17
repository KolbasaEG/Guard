using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using System.Data.Common;

internal static class IpCanonicalKeyTests
{
  public static async Task CheckMigrationAsync(ApplicationDbContext db, Action<bool, string> check)
  {
    var legacy = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO "IpAddresses" ("Id", "Address", "Status", "InsertedDate", "CreatedBy")
      VALUES ({legacy}, {" ::ffff:198.18.220.1/128 "}, {0}, {DateTime.UtcNow}, {"legacy"})
      """);
    await db.Database.MigrateAsync();
    var row = await db.IpAddresses.AsNoTracking().Where(ip => ip.Id == legacy)
      .Select(ip => new { ip.Address, Key = EF.Property<string>(ip, "CanonicalAddress") }).SingleAsync();
    check(row.Address == " ::ffff:198.18.220.1/128 " && row.Key == null,
      "canonical migration preserves legacy address and marks it unprepared");
    var factory = new PostgresTests.Factory(db.Database.GetConnectionString()!);
    var maintenance = new IpCatalogKeyMaintenance(factory);
    var batch = await maintenance.PrepareBatchAsync(1);
    check(batch.Updated == 1 && batch.Invalid == 0, "maintenance prepares migrated legacy row with application parser");
    check(await db.IpAddresses.AnyAsync(ip => ip.Id == legacy && EF.Property<string>(ip, "CanonicalAddress") == "198.18.220.1"),
      "mapped IPv6 legacy key collapses full prefix to IPv4 host");
    await db.GetService<IMigrator>().MigrateAsync("20261015174116_AddApiSnapshotConfirmation");
    check(await db.Database.SqlQueryRaw<string>("SELECT \"Address\" AS \"Value\" FROM \"IpAddresses\"").SingleAsync() == row.Address,
      "canonical migration rollback preserves original catalog data");
    await db.Database.MigrateAsync();
    check((await maintenance.PrepareBatchAsync(1)).Updated == 1, "canonical migration reapplies and resumes preparation");
    await db.IpAddresses.Where(ip => ip.Id == legacy).ExecuteDeleteAsync();
  }

  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory,
    IIpAddressManagementService root, Action<bool, string> check)
  {
    await using var db = await factory.CreateDbContextAsync();
    var ids = new List<Guid>();
    var maintenance = new IpCatalogKeyMaintenance(factory);
    try {
      var cases = new[] { " 198.18.220.199/25 ", "198.18.220.128/25", "2001:0DB8:0000:0000::1/128",
        "::ffff:198.18.220.2/128", "::ffff:198.18.220.199/121", "not-an-ip", "01.2.3.4", "198.18.220.3" };
      foreach (var address in cases) {
        var id = Guid.NewGuid(); ids.Add(id);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
          INSERT INTO "IpAddresses" ("Id", "Address", "Status", "InsertedDate", "CreatedBy")
          VALUES ({id}, {address}, {(int)Status.Archived}, {DateTime.UtcNow}, {"legacy"})
          """);
      }
      await PostgresTests.ThrowsAsync<EntityRuleException>(() => root.CreateAsync(new("198.18.221.1", null, null)),
        check, "unprepared legacy catalog blocks writes rather than bypassing duplicate checks");
      check(await root.GetDetailsAsync(ids[0]) != null, "legacy catalog remains readable during preparation");
      using (var cancelled = new CancellationTokenSource()) {
        cancelled.Cancel();
        await PostgresTests.ThrowsAsync<OperationCanceledException>(() => maintenance.PrepareBatchAsync(2, cancelled.Token),
          check, "maintenance cancellation propagates before preparation");
      }
      var interrupted = new IpCatalogKeyMaintenance(new FaultFactory(db.Database.GetConnectionString()!));
      await PostgresTests.ThrowsAsync<IOException>(() => interrupted.PrepareBatchAsync(2), check,
        "maintenance failure after SQL save propagates without committing batch");
      check(await db.IpAddresses.CountAsync(ip => ids.Contains(ip.Id) && EF.Property<string>(ip, "CanonicalAddress") == null) == cases.Length,
        "failed batch rolls back all prepared keys and remains resumable");
      var prepared = 0; var invalid = 0;
      while (true) {
        var batch = await maintenance.PrepareBatchAsync(2);
        check(batch.Updated is >= 0 and <= 2, "maintenance bounds each committed batch");
        prepared += batch.Updated; invalid += batch.Invalid;
        if (batch.Updated == 0) break;
      }
      check(prepared == cases.Length && invalid == 2, "maintenance resumes only missing keys and preserves invalid legacy rows");
      for (var i = 0; i < cases.Length; i++) {
        var row = await db.IpAddresses.AsNoTracking().Where(ip => ip.Id == ids[i]).Select(ip => new {
          ip.Address, ip.Status, ip.CreatedBy, ip.ModifiedBy, ip.LastModifiedDate,
          Key = EF.Property<string>(ip, "CanonicalAddress"), Source = EF.Property<string>(ip, "CanonicalSource") }).SingleAsync();
        var expected = IpAddressRule.TryParse(cases[i], out var rule) ? rule!.CanonicalAddress : "";
        check(row.Key == expected && row.Source == cases[i] && row.Address == cases[i] && row.Status == Status.Archived &&
          row.CreatedBy == "legacy" && row.ModifiedBy == null && row.LastModifiedDate == null,
          "backfill preserves source/status/audit and uses exact application canonicalization");
      }
      check((await maintenance.PrepareBatchAsync()).Updated == 0, "maintenance is idempotent");
      foreach (var address in new[] { "198.18.220.150/25", "::ffff:198.18.220.2", "2001:db8::1" })
        await PostgresTests.ThrowsAsync<ArgumentException>(() => root.CreateAsync(new(address, null, null)), check,
          "indexed duplicate check includes equivalent archived legacy rows");

      var current = await root.CreateAsync(new("198.18.221.10", null, null)); ids.Add(current);
      await root.UpdateAsync(new(current, "::ffff:198.18.221.10/128", "same host", null));
      check(await db.IpAddresses.AnyAsync(ip => ip.Id == current && ip.Address == "198.18.221.10" &&
        EF.Property<string>(ip, "CanonicalAddress") == "198.18.221.10"), "indexed update excludes its own ID and persists prepared key");
      var tracked = await db.IpAddresses.SingleAsync(ip => ip.Id == current);
      tracked.Address = "2001:0db8:abcd::1/128";
      db.SaveChanges(); db.ChangeTracker.Clear();
      check(await db.IpAddresses.AnyAsync(ip => ip.Id == current && EF.Property<string>(ip, "CanonicalAddress") == "2001:db8:abcd::1"),
        "synchronous context saves also refresh canonical key");
      try {
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"IpAddresses\" SET \"Address\" = {"198.18.221.11"} WHERE \"Id\" = {current}");
        check(false, "source constraint must reject SQL address changes with stale metadata");
      }
      catch (PostgresException ex) {
        check(ex.SqlState == PostgresErrorCodes.CheckViolation && ex.ConstraintName == "CK_IpAddresses_CanonicalSource",
          "source constraint rejects SQL address changes with stale metadata");
      }

      async Task<bool> CreateConcurrentAsync(string address) {
        try { var id = await root.CreateAsync(new(address, null, null)); lock (ids) ids.Add(id); return true; }
        catch (ArgumentException) { return false; }
      }
      var outcomes = await Task.WhenAll(CreateConcurrentAsync("198.18.222.199/25"), CreateConcurrentAsync("::ffff:198.18.222.150/121"));
      check(outcomes.Count(v => v) == 1, "catalog lock allows only one of two concurrent equivalent creates");
      await CheckIndexPlansAsync(db, check);
    }
    finally {
      db.ChangeTracker.Clear();
      await db.IpAddresses.Where(ip => ids.Contains(ip.Id)).ExecuteDeleteAsync();
    }
  }

  private static async Task CheckIndexPlansAsync(ApplicationDbContext db, Action<bool, string> check)
  {
    await using var transaction = await db.Database.BeginTransactionAsync();
    await db.Database.ExecuteSqlRawAsync("SET LOCAL enable_seqscan = off");
    var canonical = await db.Database.SqlQueryRaw<string>("""
      EXPLAIN (FORMAT JSON) SELECT 1 FROM "IpAddresses" WHERE "CanonicalAddress" = '198.18.220.128/25' LIMIT 1
      """).ToListAsync();
    var missing = await db.Database.SqlQueryRaw<string>("""
      EXPLAIN (FORMAT JSON) SELECT 1 FROM "IpAddresses" WHERE "CanonicalAddress" IS NULL LIMIT 1
      """).ToListAsync();
    check(string.Join("", canonical).Contains("IX_IpAddresses_CanonicalAddress"), "canonical equality supports B-tree index");
    check(string.Join("", missing).Contains("IX_IpAddresses_"), "missing-key check supports indexed access");
    await transaction.RollbackAsync();
  }

  private sealed class FaultFactory(string connection) : IDbContextFactory<ApplicationDbContext>
  {
    public ApplicationDbContext CreateDbContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
      .UseNpgsql(connection).AddInterceptors(new FailAfterSave()).Options);
  }

  private sealed class FailAfterSave : DbCommandInterceptor
  {
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result,
      CancellationToken cancellationToken = default) => command.CommandText.Contains("FROM unnest(@ids, @keys, @sources)")
        ? throw new IOException("Synthetic preparation failure after SQL update") : ValueTask.FromResult(result);
  }
}
