using Guard.Core.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Guard.Core.Contexts;

/// <summary>Явная подготовка ключей в окно обслуживания; не запускается при старте приложения.</summary>
public sealed class IpCatalogKeyMaintenance(IDbContextFactory<ApplicationDbContext> factory)
{
  public sealed record BatchResult(int Updated, int Invalid);

  public async Task<BatchResult> PrepareBatchAsync(int batchSize = 2000, CancellationToken ct = default)
  {
    if (batchSize is < 1 or > 10000) throw new ArgumentOutOfRangeException(nameof(batchSize));
    await using var db = await factory.CreateDbContextAsync(ct);
    await using var transaction = await db.Database.BeginTransactionAsync(ct);
    await IpAddressService.LockCatalogAsync(db, ct);
    // Reads remain available; no concurrent SQL writer can change a selected source within a batch.
    await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"IpAddresses\" IN SHARE ROW EXCLUSIVE MODE", ct);
    var rows = await db.IpAddresses.AsNoTracking()
      .Where(ip => EF.Property<string>(ip, IpCatalogKeys.Canonical) == null)
      .OrderBy(ip => ip.Id).Take(batchSize).Select(ip => new { ip.Id, ip.Address }).ToListAsync(ct);
    var keys = rows.Select(row => IpCatalogKeys.Get(row.Address)).ToArray();
    if (rows.Count > 0) {
      // One parameterized update per bounded batch, not one SQL command per legacy row.
      var changed = await db.Database.ExecuteSqlRawAsync("""
        UPDATE "IpAddresses" AS ip
        SET "CanonicalAddress" = batch.key, "CanonicalSource" = batch.source
        FROM unnest(@ids, @keys, @sources) AS batch(id, key, source)
        WHERE ip."Id" = batch.id
        """, new object[] {
          new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = rows.Select(row => row.Id).ToArray() },
          new NpgsqlParameter("keys", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = keys },
          new NpgsqlParameter("sources", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = rows.Select(row => row.Address).ToArray() }
        }, ct);
      if (changed != rows.Count) throw new InvalidOperationException("Пакет подготовки IP изменился во время записи.");
    }
    await transaction.CommitAsync(ct);
    return new(rows.Count, keys.Count(key => key.Length == 0));
  }
}
