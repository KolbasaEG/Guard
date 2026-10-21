using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services.Notifications;

public sealed class NotificationDispatcher(IDbContextFactory<ApplicationDbContext> factory, INotificationSignals signals)
{
  // Единственный порядок публикации версий, в том числе при ручном повторе обработки.
  private const long DeliveryLock = 73460225;
  public async Task<int> DeliverAsync(CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({DeliveryLock})", ct);
    var events = await db.NotificationEvents.Where(e => e.ProcessedAtUtc == null)
      .OrderBy(e => e.OccurredAtUtc).ThenBy(e => e.Id).Take(100).ToListAsync(ct);
    var affected = new HashSet<string>(StringComparer.Ordinal);
    foreach (var item in events) {
      if (item.SourceKey == null) {
        if (item.UserId != null && await db.Users.AnyAsync(u => u.Id == item.UserId, ct)) {
          var revision = await NextRevisionAsync(db, ct);
          db.UserNotifications.Add(Create(item, item.UserId, revision)); affected.Add(item.UserId);
        }
      } else await IncidentAsync(db, item, affected, ct);
      item.ProcessedAtUtc = DateTime.UtcNow;
      // Последующее событие в том же batch должно видеть созданный инцидент.
      await db.SaveChangesAsync(ct);
    }
    await tx.CommitAsync(ct);
    foreach (var userId in affected) signals.Invalidate(userId);
    return events.Count;
  }

  private static Task<long> NextRevisionAsync(ApplicationDbContext db, CancellationToken ct) =>
    db.Database.SqlQueryRaw<long>("SELECT nextval('\"NotificationRevision\"') AS \"Value\"").SingleAsync(ct);

  private static UserNotification Create(NotificationEvent item, string userId, long revision) => new() {
    UserId = userId, EventKey = item.EventKey, Kind = item.Kind, Severity = item.Severity,
    Title = item.Title, Message = item.Message, Target = item.Target, TargetId = item.TargetId,
    CreatedAtUtc = item.OccurredAtUtc, UpdatedAtUtc = item.OccurredAtUtc, Revision = revision
  };

  private static async Task IncidentAsync(ApplicationDbContext db, NotificationEvent item, HashSet<string> affected, CancellationToken ct)
  {
    var incident = await db.NotificationIncidents.SingleOrDefaultAsync(i => i.SourceKey == item.SourceKey && i.Active, ct);
    if (item.Recovery && (incident == null || item.Kind == UserNotificationKind.ScheduledSynchronization && !incident.Covers(item.UpdatedSince))) return;
    var created = incident == null;
    if (incident == null) {
      incident = new() { SourceKey = item.SourceKey!, Kind = item.Kind, FirstSeenUtc = item.OccurredAtUtc,
        LastSeenUtc = item.OccurredAtUtc, RequiredSince = item.UpdatedSince, FullSnapshotRequired = item.UpdatedSince == null };
      db.NotificationIncidents.Add(incident);
    } else {
      incident.LastSeenUtc = item.OccurredAtUtc;
      if (item.Recovery) { incident.Active = false; incident.ResolvedAtUtc = item.OccurredAtUtc; }
      else {
        incident.Occurrences++;
        incident.FullSnapshotRequired |= item.UpdatedSince == null;
        if (item.UpdatedSince.HasValue && (!incident.RequiredSince.HasValue || item.UpdatedSince < incident.RequiredSince))
          incident.RequiredSince = item.UpdatedSince;
      }
    }
    var rootIds = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
      where r.NormalizedName == "ROOT" select ur.UserId).Distinct().ToListAsync(ct);
    var rows = await db.UserNotifications.Where(n => n.IncidentId == incident.Id).ToListAsync(ct);
    long? revision = created || item.Recovery ? await NextRevisionAsync(db, ct) : null;
    foreach (var row in rows) {
      row.IncidentActive = incident.Active;
      row.Message = item.Message + (item.Recovery ? "" : $" Повторений: {incident.Occurrences}.");
      row.TargetId = item.TargetId; row.Severity = item.Severity;
      if (item.Recovery) {
        row.Title = item.Title; row.UpdatedAtUtc = item.OccurredAtUtc; row.Revision = revision!.Value;
        row.ReadAtUtc = null;
      }
      affected.Add(row.UserId);
    }
    foreach (var userId in rootIds.Except(rows.Select(n => n.UserId), StringComparer.Ordinal)) {
      revision ??= await NextRevisionAsync(db, ct);
      var row = Create(item, userId, revision.Value);
      row.EventKey = "incident:" + incident.Id; row.RootOnly = true; row.IncidentId = incident.Id;
      row.IncidentActive = incident.Active; row.CreatedAtUtc = incident.FirstSeenUtc;
      db.UserNotifications.Add(row); affected.Add(userId);
    }
  }

  public async Task CleanupAsync(DateTime utcNow, CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({DeliveryLock})", ct);
    var cutoff = utcNow.AddDays(-90);
    await db.UserNotifications.Where(n => !n.IncidentActive && n.ReadRevision == n.Revision && n.ReadAtUtc < cutoff).ExecuteDeleteAsync(ct);
    await db.NotificationIncidents.Where(i => !i.Active && i.ResolvedAtUtc < cutoff && !db.UserNotifications.Any(n => n.IncidentId == i.Id)).ExecuteDeleteAsync(ct);
    // Ключ остаётся tombstone: поздняя доставка не должна воссоздать удалённую историю.
    await db.NotificationEvents.Where(e => e.ProcessedAtUtc < cutoff && e.Message != "").ExecuteUpdateAsync(s => s
      .SetProperty(e => e.Message, "").SetProperty(e => e.Title, "").SetProperty(e => e.UserId, (string?)null)
      .SetProperty(e => e.SourceKey, (string?)null).SetProperty(e => e.TargetId, (Guid?)null), ct);
    await tx.CommitAsync(ct);
  }
}
