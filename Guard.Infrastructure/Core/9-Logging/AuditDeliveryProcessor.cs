using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Logging;

public sealed class AuditDeliveryProcessor(IDbContextFactory<ApplicationDbContext> factory, IAuditTransport transport,
  AuditHealth health, InternalLogWriter diagnostics)
{
  public async Task<int> ProcessAsync(CancellationToken ct)
  {
    var processed = 0;
    while (processed < 25 && !ct.IsCancellationRequested) {
      var token = Guid.NewGuid(); var now = DateTime.UtcNow;
      await using var db = await factory.CreateDbContextAsync(ct); db.Database.SetCommandTimeout(5);
      AuditEventRecord? entry;
      await using (var tx = await db.Database.BeginTransactionAsync(ct)) {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({AuditJournal.QueueLock})", ct);
        var settings = await db.AuditSettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct);
        if (!settings.Configured || settings.Mode != AuditMode.Siem) return processed;
        var claimed = await db.AuditEvents.FromSqlInterpolated($"""
          WITH candidate AS (
            SELECT "EventId" FROM "AuditEvents" WHERE "DeliveryRequired" AND "SentAtUtc" IS NULL
              AND "NextAttemptAtUtc" <= {now} AND ("LeaseUntilUtc" IS NULL OR "LeaseUntilUtc" < {now})
            ORDER BY "NextAttemptAtUtc", "EventId" FOR UPDATE SKIP LOCKED LIMIT 1
          )
          UPDATE "AuditEvents" AS a SET "LeaseToken"={token},"LeaseUntilUtc"={now.AddMinutes(2)},"Attempts"="Attempts"+1
          FROM candidate AS c WHERE a."EventId"=c."EventId" RETURNING a.*
          """).AsNoTracking().ToListAsync(ct);
        entry = claimed.SingleOrDefault();
        if (entry != null) {
          await db.AuditDeliveryAttempts.Where(a => a.EventId == entry.EventId && a.CompletedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.CompletedAtUtc, now).SetProperty(a => a.Error, "LeaseExpired; outcome unknown"), ct);
          db.AuditDeliveryAttempts.Add(new() { Id = token, EventId = entry.EventId, StartedAtUtc = now,
            Destination = settings.Host + ":" + settings.Port });
          await db.SaveChangesAsync(ct);
        }
        await db.AuditSettings.Where(s => s.Id == 1).ExecuteUpdateAsync(s => s.SetProperty(p => p.WorkerCompletedAtUtc, now), ct);
        await tx.CommitAsync(ct);
      }
      if (entry == null) break;
      processed++;
      string? failure = null;
      try { await transport.SendAsync(entry, ct); }
      catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
      catch (Exception error) { failure = error.GetType().Name; }
      var completed = DateTime.UtcNow;
      await using (var tx = await db.Database.BeginTransactionAsync(ct)) {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({AuditJournal.QueueLock})", ct);
        if (failure == null) {
          var updated = await db.AuditEvents.Where(e => e.EventId == entry.EventId && e.LeaseToken == token)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.SentAtUtc, completed).SetProperty(e => e.LastError, (string?)null)
              .SetProperty(e => e.LeaseToken, (Guid?)null).SetProperty(e => e.LeaseUntilUtc, (DateTime?)null), ct);
          if (updated == 1) health.Sent();
        } else {
          var retry = completed.AddSeconds(Math.Min(3600, 5 * Math.Pow(2, Math.Min(entry.Attempts, 10))));
          await db.AuditEvents.Where(e => e.EventId == entry.EventId && e.LeaseToken == token)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.NextAttemptAtUtc, retry).SetProperty(e => e.LastError, failure)
              .SetProperty(e => e.LeaseToken, (Guid?)null).SetProperty(e => e.LeaseUntilUtc, (DateTime?)null), ct);
          if (entry.Attempts == 1 || entry.Attempts % 10 == 0)
            diagnostics.Write($"AuditDeliveryDelayed EventId={entry.EventId} Attempts={entry.Attempts} ErrorType={failure}");
        }
        await db.AuditDeliveryAttempts.Where(a => a.Id == token).ExecuteUpdateAsync(s => s
          .SetProperty(a => a.CompletedAtUtc, completed).SetProperty(a => a.Succeeded, failure == null)
          .SetProperty(a => a.Error, failure), ct);
        await db.AuditSettings.Where(s => s.Id == 1).ExecuteUpdateAsync(s => s.SetProperty(p => p.WorkerCompletedAtUtc, completed), ct);
        await tx.CommitAsync(ct);
      }
    }
    await using var summary = await factory.CreateDbContextAsync(ct);
    var pending = summary.AuditEvents.Where(e => e.DeliveryRequired && e.SentAtUtc == null);
    health.Queue(await pending.CountAsync(ct), await pending.Select(e => e.QueuedAtUtc).MinAsync(ct));
    return processed;
  }
}
