using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Guard.Core.Services.Notifications;
using Guard.Web.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;

internal static class NotificationTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var signals = new NotificationSignals(NullLogger<NotificationSignals>.Instance);
    var first = 0; var second = 0;
    using (signals.Subscribe("owner", () => first++)) {
      using (signals.Subscribe("other", () => second++)) {
        using var broken = signals.Subscribe("owner", () => throw new InvalidOperationException("test subscriber"));
        signals.Invalidate("owner");
        check(first == 1 && second == 0, "notification invalidation isolates recipients and failing subscribers");
      }
    }
    signals.Invalidate("owner"); check(first == 1, "disposed circuit releases notification subscription");

    await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
      .UseNpgsql("Host=127.0.0.1;Port=1;Database=unused;Username=unused").Options);
    var source = new SynchronizationSettings { ApiBaseUrl = "https://directory.invalid/", ApiHeadSubdivisionId = 42 };
    var key = NotificationOutbox.SynchronizationSource(source);
    source.ApiBaseUrl = source.ApiBaseUrl.TrimEnd('/');
    check(NotificationOutbox.SynchronizationSource(source) == key, "source identity ignores trailing slash");
    source.ApiUsername = "different";
    check(NotificationOutbox.SynchronizationSource(source) != key, "different API account cannot resolve another source scope's incident");
    source.ApiHeadSubdivisionId = 43;
    check(NotificationOutbox.SynchronizationSource(source) != key, "different source scope cannot resolve prior incident");
    NotificationOutbox.Synchronization(db, new() { Status = SynchronizationStatus.Queued, ActorId = "owner" });
    NotificationOutbox.Synchronization(db, new() { Status = SynchronizationStatus.Succeeded, Scheduled = true });
    check(!db.ChangeTracker.Entries<NotificationEvent>().Any(), "queued operations and ordinary scheduled success do not publish personal notifications");
    NotificationOutbox.Synchronization(db, new() { Status = SynchronizationStatus.Cancelled, ActorId = "owner" });
    check(db.ChangeTracker.Entries<NotificationEvent>().Single().Entity.UserId == "owner", "manual cancellation publishes only to initiator");
    db.ChangeTracker.Clear();

    var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!;
    var initializer = db.GetService<IModelRuntimeInitializer>();
    var oldModel = initializer.Initialize(snapshot.Model, designTime: true);
    var model = db.GetService<IDesignTimeModel>().Model;
    var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(oldModel.GetRelationalModel(), model.GetRelationalModel());
    check(!differences.Any(op => op is CreateTableOperation { Name: "UserNotifications" or "NotificationEvents" or "NotificationIncidents" } ||
      op is AddColumnOperation { Name: "NotificationSourceKey" } || op is CreateSequenceOperation { Name: "NotificationRevision" } ||
      op is AlterColumnOperation { Table: "UserNotifications" or "NotificationEvents" or "NotificationIncidents" } ||
      op is CreateIndexOperation { Table: "UserNotifications" or "NotificationEvents" or "NotificationIncidents" }),
      "notification model snapshot matches new entities, source identity, sequence and indexes");

    var permissions = new PermissionsStub("owner", false);
    var service = new ServiceStub();
    await using var center = new NotificationCenterState(service, permissions, signals, NullLogger<NotificationCenterState>.Instance);
    var arrivals = new List<long>(); center.Arrived += n => arrivals.Add(n.Revision);
    service.Rows.Add(Row(1)); await center.RefreshAsync();
    check(arrivals.Count == 0 && center.Summary?.UnreadCount == 1, "initial center load counts historical unread without toast");
    service.Rows.Add(Row(2)); await center.RefreshAsync(); await center.RefreshAsync();
    check(arrivals.SequenceEqual(new long[] { 2 }), "online delivery produces one toast per revision per circuit");
    service.Fail = true; await center.RefreshAsync();
    check(center.Summary == null && center.Error != null, "unavailable center clears unreliable count instead of claiming zero");
    service.Fail = false; await center.RefreshAsync();
    check(arrivals.Count == 1 && center.Summary?.UnreadCount == 2, "poll recovery restores state without replaying acknowledged toast revisions");
    service.Rows.Add(Row(3) with { IsRead = true }); await center.RefreshAsync();
    check(arrivals.Count == 1, "reading in another tab suppresses toast for already read version");
  }

  private static UserNotificationDto Row(long revision) => new(Guid.NewGuid(), UserNotificationKind.ExportReady, UserNotificationSeverity.Success,
    "Выгрузка готова", "Готово", NotificationTarget.Export, Guid.NewGuid(), false, false, DateTime.UtcNow, DateTime.UtcNow, revision, false);

  internal sealed class PermissionsStub(string id, bool root) : IPermissionService
  {
    public bool Root { get; set; } = root;
    public string UserId { get; set; } = id;
    public bool Denied { get; set; }
    public HashSet<string> Codes { get; } = [];
    public Action? BeforeRead { get; set; }
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) {
      BeforeRead?.Invoke();
      return Denied ? throw new UnauthorizedAccessException() : Task.FromResult(new UserAccessSnapshot(UserId, Root, new(Codes)));
    }
    public Task<UserAccessSnapshot> GetForUserAsync(string userId, CancellationToken ct = default) => GetCurrentAsync(ct);
    public async Task<bool> HasAsync(string permission, CancellationToken ct = default) => (await GetCurrentAsync(ct)).Has(permission);
    public async Task RequireAsync(string permission, CancellationToken ct = default) { if (!await HasAsync(permission, ct)) throw new UnauthorizedAccessException(); }
  }
  private sealed class ServiceStub : IUserNotificationService
  {
    public List<UserNotificationDto> Rows { get; } = [];
    public bool Fail { get; set; }
    public Task<NotificationSummary> GetSummaryAsync(CancellationToken ct = default) => Fail ? throw new IOException() :
      Task.FromResult(new NotificationSummary(Rows.Count(n => !n.IsRead), Rows.Select(n => n.Revision).DefaultIfEmpty().Max(), Rows.TakeLast(10).Reverse().ToArray()));
    public Task<NotificationPage> SearchAsync(NotificationSearch q, CancellationToken ct = default) {
      var rows = Rows.Where(n => n.Revision > q.AfterRevision && n.Revision <= q.ThroughRevision).ToArray();
      return Task.FromResult(new NotificationPage(rows.Skip(q.Skip).Take(q.Take).ToArray(), rows.Length));
    }
    public Task<UserNotificationDto?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Rows.SingleOrDefault(n => n.Id == id));
    public Task<UserNotificationDto> GetAccessibleTargetAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task MarkReadAsync(Guid id, long revision, CancellationToken ct = default) => throw new NotSupportedException();
    public Task MarkAllReadAsync(long watermark, CancellationToken ct = default) => throw new NotSupportedException();
  }
}
