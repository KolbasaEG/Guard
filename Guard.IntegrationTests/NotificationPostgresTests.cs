using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Guard.Core.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

internal static class NotificationPostgresTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    var signals = new NotificationSignals(NullLogger<NotificationSignals>.Instance);
    var dispatcher = new NotificationDispatcher(factory, signals);
    var ownerPermission = new NotificationTests.PermissionsStub("notification-owner", false);
    var rootPermission = new NotificationTests.PermissionsStub("notification-root", true);
    var otherPermission = new NotificationTests.PermissionsStub("notification-other", false);
    var owner = new UserNotificationService(factory, ownerPermission, signals, new ExportAccess(factory, ownerPermission));
    var other = new UserNotificationService(factory, otherPermission, signals, new ExportAccess(factory, otherPermission));
    var root = new UserNotificationService(factory, rootPermission, signals, new ExportAccess(factory, rootPermission));
    await using (var setup = await factory.CreateDbContextAsync()) {
      setup.Users.AddRange(new[] { "notification-owner", "notification-other", "notification-root" }.Select(id => new ApplicationUser {
        Id = id, UserName = id, NormalizedUserName = id.ToUpperInvariant(), SecurityStamp = "test"
      }));
      setup.UserRoles.Add(new() { UserId = "notification-root", RoleId = "root-role" });
      await setup.SaveChangesAsync();
    }
    // Очередь может содержать результаты предыдущих сценариев набора.
    while (await dispatcher.DeliverAsync() > 0) { }
    var rollbackJob = new ExportJob { AuthorId = ownerPermission.UserId, RequestJson = "{}", ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1) };
    await using (var db = await factory.CreateDbContextAsync()) {
      await using var tx = await db.Database.BeginTransactionAsync();
      db.ExportJobs.Add(rollbackJob); NotificationOutbox.Export(db, rollbackJob, true);
      await db.SaveChangesAsync(); await tx.RollbackAsync();
    }
    await using (var db = await factory.CreateDbContextAsync()) {
      check(!await db.ExportJobs.AnyAsync(j => j.Id == rollbackJob.Id) && !await db.NotificationEvents.AnyAsync(e => e.TargetId == rollbackJob.Id),
        "rollback removes both operation result and its notification event");
    }
    var job = new ExportJob { AuthorId = ownerPermission.UserId, State = ExportJobState.Ready, SecurityStamp = "test",
      Area = ExportArea.Personals, Format = ExportFormat.Excel,
      RequestJson = System.Text.Json.JsonSerializer.Serialize(new ExportRequest(ExportArea.Personals, ExportFormat.Excel)),
      ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(1) };
    await using (var db = await factory.CreateDbContextAsync()) {
      db.ExportJobs.Add(job); NotificationOutbox.Export(db, job, true); await db.SaveChangesAsync();
    }
    var notifiedAfterCommit = false;
    using var subscription = signals.Subscribe(ownerPermission.UserId, () => {
      using var db = factory.CreateDbContext();
      notifiedAfterCommit = db.UserNotifications.Any(n => n.TargetId == job.Id) && db.NotificationEvents.Any(e => e.TargetId == job.Id && e.ProcessedAtUtc != null);
    });
    await Task.WhenAll(dispatcher.DeliverAsync(), dispatcher.DeliverAsync());
    var ownSummary = await owner.GetSummaryAsync(); var ownRow = ownSummary.Latest.Single();
    check(notifiedAfterCommit && ownSummary.UnreadCount == 1, "concurrent delivery publishes exactly once and signals only after commit");
    check(await other.GetAsync(ownRow.Id) == null && await root.GetAsync(ownRow.Id) == null, "even Root cannot read another user's notification by forged ID");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => owner.GetAccessibleTargetAsync(ownRow.Id), check,
      "notification target rechecks revoked export permission before opening operation metadata");
    ownerPermission.Codes.Add(Guard.Core.Identity.Permissions.Personals.Export);
    ownerPermission.Codes.Add(Guard.Core.Identity.Permissions.Personals.Read);
    check((await owner.GetAccessibleTargetAsync(ownRow.Id)).TargetId == job.Id, "authorized notification target resolves the exact author's export");
    var permissionReads = 0;
    ownerPermission.BeforeRead = () => { if (++permissionReads == 3) ownerPermission.Codes.Remove(Guard.Core.Identity.Permissions.Personals.Export); };
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => owner.GetAccessibleTargetAsync(ownRow.Id), check,
      "permission revoked during notification target resolution is rechecked before return");
    ownerPermission.BeforeRead = null; ownerPermission.Codes.Add(Guard.Core.Identity.Permissions.Personals.Export);
    await using (var db = await factory.CreateDbContextAsync()) {
      db.ExportArtifacts.Add(new() { JobId = job.Id, StorageKey = "notification-test", SubdivisionIdsJson = System.Text.Json.JsonSerializer.Serialize(new[] { Guid.NewGuid() }) });
      await db.SaveChangesAsync();
    }
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => owner.GetAccessibleTargetAsync(ownRow.Id), check,
      "notification target rechecks archived export subdivision scope");
    await other.MarkReadAsync(ownRow.Id, ownRow.Revision); await root.MarkReadAsync(ownRow.Id, ownRow.Revision);
    check((await owner.GetSummaryAsync()).UnreadCount == 1, "forged read command cannot acknowledge another user's notification");
    await owner.MarkReadAsync(ownRow.Id, ownRow.Revision);
    check((await owner.GetSummaryAsync()).UnreadCount == 0 && (await owner.SearchAsync(new(true))).Count == 0,
      "explicit current-version read updates unread summary and server filter");
    check(await dispatcher.DeliverAsync() == 0 && (await owner.SearchAsync(new())).Count == 1, "retry after delivery acknowledgement does not duplicate history");

    var incidentKey = "notification-test:scheduled";
    async Task Publish(bool recovery, DateOnly? since, string source = "notification-test:scheduled") {
      await using var db = await factory.CreateDbContextAsync();
      db.NotificationEvents.Add(new() {
        EventKey = "test:" + Guid.NewGuid(), SourceKey = source, Kind = UserNotificationKind.ScheduledSynchronization,
        Title = recovery ? "Восстановлено" : "Сбой", Message = "Проверка", Recovery = recovery, UpdatedSince = since,
        Target = NotificationTarget.Synchronization, Severity = recovery ? UserNotificationSeverity.Success : UserNotificationSeverity.Error,
        OccurredAtUtc = DateTime.UtcNow
      }); await db.SaveChangesAsync(); await dispatcher.DeliverAsync();
    }
    await Publish(false, new(2026, 10, 18));
    var incidentRow = (await root.SearchAsync(new())).Items.Single(n => n.IsIncident && n.Title == "Сбой");
    await root.MarkReadAsync(incidentRow.Id, incidentRow.Revision);
    await Publish(false, new(2026, 10, 19));
    var repeat = (await root.GetAsync(incidentRow.Id))!;
    check(repeat.IsRead && repeat.Revision == incidentRow.Revision && repeat.UpdatedAtUtc == incidentRow.UpdatedAtUtc && repeat.IncidentActive,
      "repeated failure updates same incident without reordering, new toast or unread reset");
    check((await other.SearchAsync(new())).Count == 0 && (await owner.SearchAsync(new())).Items.All(n => !n.IsIncident), "operational incidents are delivered only to Root accounts");
    await Publish(true, new(2026, 10, 19));
    check((await root.GetAsync(incidentRow.Id))!.IncidentActive, "incomplete incremental coverage cannot resolve older failure");
    await Publish(true, null, "notification-test:other-source");
    check((await root.GetAsync(incidentRow.Id))!.IncidentActive, "success of another source cannot resolve active incident");
    var beforeRecovery = await root.GetSummaryAsync();
    await Publish(true, new(2026, 10, 18));
    await root.MarkReadAsync(incidentRow.Id, incidentRow.Revision);
    await root.MarkAllReadAsync(beforeRecovery.Watermark);
    var recovered = (await root.GetAsync(incidentRow.Id))!;
    check(!recovered.IsRead && !recovered.IncidentActive && recovered.Revision > incidentRow.Revision,
      "recovery updates same row and survives stale open and mark-all races as unread");
    rootPermission.Root = false;
    check(await root.GetAsync(incidentRow.Id) == null && (await root.SearchAsync(new())).Count == 0 && (await root.GetSummaryAsync()).UnreadCount == 0,
      "revoking Root hides operational history and unread count on server");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => root.GetAccessibleTargetAsync(incidentRow.Id), check,
      "revoked Root cannot follow operational notification target");
    await root.MarkReadAsync(recovered.Id, recovered.Revision);
    rootPermission.Root = true;
    check(!(await root.GetAsync(recovered.Id))!.IsRead, "revoked Root cannot mark hidden incident read");
    await Publish(false, new(2026, 10, 20));
    await using (var db = await factory.CreateDbContextAsync()) {
      check(await db.NotificationIncidents.CountAsync(i => i.SourceKey == incidentKey) == 2 &&
        await db.NotificationIncidents.CountAsync(i => i.SourceKey == incidentKey && i.Active) == 1,
        "failure after recovery starts a separate incident episode");
    }

    // Старые прочитанные удаляются; непрочитанные и активный инцидент сохраняются.
    await root.MarkReadAsync(recovered.Id, recovered.Revision);
    var active = (await root.SearchAsync(new())).Items.Single(n => n.IncidentActive);
    await root.MarkReadAsync(active.Id, active.Revision);
    await using (var db = await factory.CreateDbContextAsync()) {
      await db.UserNotifications.Where(n => n.UserId == rootPermission.UserId || n.UserId == ownerPermission.UserId)
        .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, DateTime.UtcNow.AddDays(-91)));
      db.NotificationEvents.Add(new() { EventKey = "cleanup-unread", UserId = ownerPermission.UserId, Title = "Непрочитанное", Message = "Сохранить",
        Kind = UserNotificationKind.ExportFailed, Target = NotificationTarget.Export, OccurredAtUtc = DateTime.UtcNow.AddDays(-100) });
      await db.SaveChangesAsync();
    }
    await dispatcher.DeliverAsync(); await dispatcher.CleanupAsync(DateTime.UtcNow);
    check(await owner.GetAsync(ownRow.Id) == null && await root.GetAsync(recovered.Id) == null && await root.GetAsync(active.Id) != null &&
      (await owner.SearchAsync(new(true))).Count == 1, "90-day cleanup preserves unread notifications and read active incidents");
    ownerPermission.Denied = true;
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => owner.GetSummaryAsync(), check, "expired or blocked session cannot read notification summary");
    await PostgresTests.ThrowsAsync<ArgumentException>(() => other.SearchAsync(new(Take: 101)), check, "server bounds notification pagination");

    var manualInterrupted = new SynchronizationRun { Status = SynchronizationStatus.Running, ActorId = otherPermission.UserId,
      StartedAt = DateTime.UtcNow, NotificationSourceKey = "notification-test:interrupted" };
    var scheduledInterrupted = new SynchronizationRun { Status = SynchronizationStatus.Running, Scheduled = true,
      StartedAt = DateTime.UtcNow, NotificationSourceKey = "notification-test:interrupted", UpdatedSince = new(2026, 10, 18) };
    await using (var db = await factory.CreateDbContextAsync()) {
      db.SynchronizationRuns.AddRange(manualInterrupted, scheduledInterrupted); await db.SaveChangesAsync();
      await NotificationOutbox.RecoverSynchronizationAsync(db, default);
    }
    await dispatcher.DeliverAsync();
    check((await other.SearchAsync(new())).Items.Single().TargetId == manualInterrupted.Id,
      "restart recovery delivers interrupted accepted manual run to initiator");
    await using (var db = await factory.CreateDbContextAsync()) {
      check(await db.NotificationIncidents.AnyAsync(i => i.SourceKey == "notification-test:interrupted" && i.Active),
        "restart recovery turns interrupted scheduled run into a Root incident");
      var changedEvents = await db.NotificationEvents.CountAsync(e => e.TargetId == manualInterrupted.Id || e.TargetId == scheduledInterrupted.Id);
      await NotificationOutbox.RecoverSynchronizationAsync(db, default);
      check(changedEvents == 2 && await db.NotificationEvents.CountAsync(e => e.TargetId == manualInterrupted.Id || e.TargetId == scheduledInterrupted.Id) == changedEvents,
        "repeated restart recovery does not create duplicate terminal events");
      // Последующие проверки последнего Root должны видеть исходный состав пользователей.
      var testIds = new[] { ownerPermission.UserId, otherPermission.UserId, rootPermission.UserId };
      var incidentIds = db.NotificationIncidents.Where(i => i.SourceKey.StartsWith("notification-test:")).Select(i => i.Id);
      await db.UserNotifications.Where(n => testIds.Contains(n.UserId) || n.IncidentId != null && incidentIds.Contains(n.IncidentId.Value)).ExecuteDeleteAsync();
      await db.NotificationIncidents.Where(i => i.SourceKey.StartsWith("notification-test:")).ExecuteDeleteAsync();
      await db.NotificationEvents.Where(e => e.EventKey.StartsWith("test:") || e.EventKey == "cleanup-unread" ||
        e.TargetId == job.Id || e.TargetId == manualInterrupted.Id || e.TargetId == scheduledInterrupted.Id).ExecuteDeleteAsync();
      await db.ExportArtifacts.Where(a => a.JobId == job.Id).ExecuteDeleteAsync();
      await db.ExportJobs.Where(j => j.Id == job.Id).ExecuteDeleteAsync();
      await db.SynchronizationRuns.Where(r => r.Id == manualInterrupted.Id || r.Id == scheduledInterrupted.Id).ExecuteDeleteAsync();
      await db.Users.Where(u => testIds.Contains(u.Id)).ExecuteDeleteAsync();
    }
  }
}
