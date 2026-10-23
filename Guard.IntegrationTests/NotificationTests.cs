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
using Radzen;
using System.Reflection;

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

    await CheckPageFeedbackAsync(check);
    await CheckSummaryChangesAsync(check);
  }

  private static async Task CheckSummaryChangesAsync(Action<bool, string> check)
  {
    var service = new ServiceStub();
    service.Rows.AddRange(Enumerable.Range(1, 15).Select(n => Row(n) with { IsRead = true }));
    var permissions = new PermissionsStub("owner", false);
    await using var center = new NotificationCenterState(service, permissions,
      new NotificationSignals(NullLogger<NotificationSignals>.Instance), NullLogger<NotificationCenterState>.Instance);
    var changes = 0; center.Changed += () => changes++;
    await center.RefreshAsync(); await center.RefreshAsync(); await center.RefreshAsync();
    check(changes == 1, "unchanged notification polls do not emit redundant Changed events");
    service.Rows[0] = service.Rows[0] with { IsRead = false };
    await center.RefreshAsync();
    check(changes == 2 && center.Summary?.UnreadCount == 1, "reading state outside the latest ten is detected through the unread count");
    service.Rows[0] = service.Rows[0] with { IsRead = true };
    await center.RefreshAsync();
    service.Rows.RemoveAt(0); await center.RefreshAsync();
    check(changes == 4 && center.Summary?.TotalCount == 14 && center.Summary.Watermark == 15,
      "removing an old read notification changes history even when latest entries and watermark stay the same");
    permissions.Root = true; await center.RefreshAsync(); await center.RefreshAsync();
    check(changes == 5, "access context changes still invalidate the list when the summary values are equal");
    service.Fail = true; await center.RefreshAsync(); await center.RefreshAsync();
    check(changes == 6 && center.Summary == null && center.Error != null, "an outage emits one change and repeated identical failures do not reload history");
    service.Fail = false; await center.RefreshAsync();
    check(changes == 7 && center.Summary?.TotalCount == 14 && center.Error == null, "recovery emits a change and restores the summary");
    service.Rows[^1] = service.Rows[^1] with { Title = "Обновлённый текст" };
    await center.RefreshAsync();
    check(changes == 8 && center.Summary!.Latest[0].Title == "Обновлённый текст", "content changes are detected independently of counters and watermark");
  }

  private static async Task CheckPageFeedbackAsync(Action<bool, string> check)
  {
    const string style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;";
    var service = new ServiceStub(); service.Rows.Add(Row(1));
    var signals = new NotificationSignals(NullLogger<NotificationSignals>.Instance);
    await using var center = new NotificationCenterState(service, new PermissionsStub("owner", false), signals, NullLogger<NotificationCenterState>.Instance);
    await center.RefreshAsync();
    var notifications = new NotificationService();
    using var page = new Guard.Components.Pages.Notifications.Index();
    Inject(page, "Center", center); Inject(page, "Notifications", notifications);
    Inject(page, "Logger", NullLogger<Guard.Components.Pages.Notifications.Index>.Instance);
    await Call(page, "ReadAllAsync");
    check(service.ReadAllCalls == 1 && service.Rows.All(n => n.IsRead) && center.Summary?.UnreadCount == 0 &&
      notifications.Messages.Count == 1 && notifications.Messages[0].Severity == NotificationSeverity.Success && notifications.Messages[0].Style == style,
      "mark all read notifies success only after the service marks the snapshot and refreshes the summary");

    notifications.Messages.Clear(); service.FailReadAll = true;
    await Call(page, "ReadAllAsync");
    check(notifications.Messages.Count == 1 && notifications.Messages[0].Severity == NotificationSeverity.Error &&
      !notifications.Messages[0].Detail.Contains("private database") && notifications.Messages[0].Style == style,
      "failed mark all read produces one safe error notification without claiming success");

    notifications.Messages.Clear(); service.FailReadAll = false;
    service.ReadAllGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    var pending = Call(page, "ReadAllAsync");
    var beforeDuplicate = service.ReadAllCalls;
    await Call(page, "ReadAllAsync");
    check(service.ReadAllCalls == beforeDuplicate && notifications.Messages.Count == 0, "repeated mark all read is blocked while the first operation is pending");
    page.Dispose(); service.ReadAllGate.SetResult(); await pending;
    check(notifications.Messages.Count == 0, "mark all read completing after page disposal emits no late notification");

    using var details = new Guard.Components.Pages.Notifications.Details();
    Inject(details, "Service", service); Inject(details, "Notifications", notifications);
    Inject(details, "Logger", NullLogger<Guard.Components.Pages.Notifications.Details>.Instance);
    await Call(details, "OpenTargetAsync");
    check(notifications.Messages.Count == 1 && notifications.Messages[0].Severity == NotificationSeverity.Error &&
      notifications.Messages[0].Detail.Contains("Связанное событие недоступно") && notifications.Messages[0].Style == style,
      "unavailable notification target produces a safe error notification");
    notifications.Messages.Clear(); details.Dispose(); await Call(details, "OpenTargetAsync");
    check(notifications.Messages.Count == 0, "closed notification details cannot start a target operation");
  }

  private static void Inject(object component, string property, object value) => component.GetType()
    .GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(component, value);
  private static Task Call(object component, string method) => (Task)component.GetType()
    .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component, null)!;

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
  internal sealed class ServiceStub : IUserNotificationService
  {
    public List<UserNotificationDto> Rows { get; } = [];
    public List<NotificationSearch> Searches { get; } = [];
    public bool Fail { get; set; }
    public bool FailReadAll { get; set; }
    public int ReadAllCalls { get; private set; }
    public TaskCompletionSource? ReadAllGate { get; set; }
    public Func<NotificationSearch, CancellationToken, Task<NotificationPage>>? SearchOverride { get; set; }
    public Task<NotificationSummary> GetSummaryAsync(CancellationToken ct = default) => Fail ? throw new IOException() :
      Task.FromResult(new NotificationSummary(Rows.Count(n => !n.IsRead), Rows.Select(n => n.Revision).DefaultIfEmpty().Max(), Rows.TakeLast(10).Reverse().ToArray(), Rows.Count));
    public Task<NotificationPage> SearchAsync(NotificationSearch q, CancellationToken ct = default) {
      Searches.Add(q);
      if (SearchOverride != null && q.AfterRevision == 0 && q.ThroughRevision == long.MaxValue) return SearchOverride(q, ct);
      var rows = Rows.Where(n => n.Revision > q.AfterRevision && n.Revision <= q.ThroughRevision && (!q.UnreadOnly || !n.IsRead)).ToArray();
      return Task.FromResult(new NotificationPage(rows.Skip(q.Skip).Take(q.Take).ToArray(), rows.Length));
    }
    public Task<UserNotificationDto?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Rows.SingleOrDefault(n => n.Id == id));
    public Task<UserNotificationDto> GetAccessibleTargetAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task MarkReadAsync(Guid id, long revision, CancellationToken ct = default) => throw new NotSupportedException();
    public async Task MarkAllReadAsync(long watermark, CancellationToken ct = default) {
      ReadAllCalls++;
      if (FailReadAll) throw new IOException("private database failure");
      if (ReadAllGate != null) await ReadAllGate.Task.WaitAsync(ct);
      for (var i = 0; i < Rows.Count; i++) if (Rows[i].Revision <= watermark) Rows[i] = Rows[i] with { IsRead = true };
    }
  }
}
