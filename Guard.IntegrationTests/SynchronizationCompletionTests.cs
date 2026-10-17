using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

internal static partial class ApiSynchronizationIntegrationTests
{
  private static async Task CheckCompletionAsync(IDbContextFactory<ApplicationDbContext> factory, PermissionsStub permissions,
      ClientStub api, ApiCredentialProtector credentials, ApiSynchronizationRunner normalRunner,
      SynchronizationService normalService, Action<bool, string> check)
  {
    var original = api.Snapshot;
    foreach (var failure in new[] { "none", "io", "cancelled", "stopping" }) {
      var delivery = new CompletionDelivery(failure);
      var runner = new ApiSynchronizationRunner(factory, api, permissions, delivery, delivery, credentials);
      var service = new SynchronizationService(factory, permissions, runner, delivery, credentials, delivery);
      using var saving = new CancellationTokenSource();
      delivery.Stopping = saving;
      var previous = (await service.GetOverviewAsync()).Settings;
      var time = previous.DailyTime == new TimeOnly(3, 0) ? new TimeOnly(4, 0) : new TimeOnly(3, 0);
      delivery.Observe = async (type, actor, details) => {
        await using var db = await factory.CreateDbContextAsync();
        return type == AuditEventType.SynchronizationSettingsChanged && actor == "test-api-root" &&
          details!.Contains($"DailyTime={time:HH:mm}") && await db.SynchronizationSettings.AnyAsync(s =>
            s.Id == 1 && s.DailyTime == time && s.Version != previous.Version && s.ModifiedBy == actor);
      };
      await service.SaveSettingsAsync(DataManagementMode.Api, false, time, previous.Version, saving.Token);
      check(delivery.SawCommit, "synchronization settings audit observes committed version and trusted author");
      var saved = (await service.GetOverviewAsync()).Settings;
      check(saved.Version != previous.Version && saved.DailyTime == time, "saved synchronization settings survive failed audit delivery");
      var before = delivery.Events.Count;
      await PostgresTests.ThrowsAsync<EntityConflictException>(() => service.SaveSettingsAsync(DataManagementMode.Api, false, time, previous.Version), check,
        "stale synchronization settings version remains rejected");
      check(delivery.Events.Count == before && (await service.GetOverviewAsync()).Settings.Version == saved.Version,
        "stale settings do not write data or emit completion audit");
      var trusted = permissions.Current;
      permissions.Current = new("non-root", false, []);
      try {
        await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveSettingsAsync(DataManagementMode.Local, false, time, saved.Version),
          check, "non-Root settings command remains forbidden");
        await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.RunAsync(), check,
          "non-Root manual synchronization remains forbidden");
      }
      finally { permissions.Current = trusted; }
      check(delivery.Events.Count == before && (await service.GetOverviewAsync()).Settings.Version == saved.Version,
        "access rejection changes neither settings nor completion events");

      var ids = new List<Guid>();
      foreach (var queued in new[] { false, true }) {
        using var running = new CancellationTokenSource();
        delivery.Stopping = running;
        var changed = JsonSerializer.Deserialize<ApiPersonal>(JsonSerializer.Serialize(original.Personnel[0]))!;
        changed.FirstName = $"Completion {failure} {queued}";
        api.Snapshot = new ApiSnapshot([], [], [changed]);
        delivery.Observe = async (type, actor, details) => {
          var id = Guid.Parse(details!.Split(';')[0][6..]);
          await using var db = await factory.CreateDbContextAsync();
          var run = await db.SynchronizationRuns.AsNoTracking().SingleAsync(r => r.Id == id);
          var settings = await db.SynchronizationSettings.AsNoTracking().SingleAsync();
          return type == AuditEventType.SynchronizationCompleted && actor == "test-api-root" && run.ActorId == actor &&
            run.Status == SynchronizationStatus.Succeeded && run.Phase == "Completed" && run.Error == null &&
            run.CompletedAt != null && settings.LastChangesSynchronizedAt == run.CompletedAt &&
            settings.ClassifiersSynchronizedAt == run.CompletedAt &&
            await db.Personals.AnyAsync(p => p.ExternalId == changed.Id && p.FirstName == changed.FirstName);
        };
        Guid id;
        if (queued) {
          var request = await service.RunAsync(updatedSince: new(2000, 8, 10), enqueue: true);
          check(request.Status == SynchronizationStatus.Queued && delivery.Events.Count == before + 1,
            "enqueue persists request without sending completion audit");
          var result = await runner.RunAsync("untrusted-argument", null, false, running.Token, queuedRunId: request.Id);
          id = result.Id;
          check(result.Status == SynchronizationStatus.Succeeded && result.ActorId == "test-api-root" && result.Id == request.Id,
            "queued runner returns committed success and stored author after audit failure");
        }
        else {
          var result = await service.RunAsync(running.Token, new(2000, 8, 10));
          id = result.Id;
          check(result.Status == SynchronizationStatus.Succeeded && result.Error == null,
            "manual service returns successful DTO despite audit delivery failure");
        }
        ids.Add(id);
        check(delivery.SawCommit, "synchronization audit sees committed data, Succeeded and actuality in independent context");
        await using (var db = await factory.CreateDbContextAsync()) {
          var persisted = await db.SynchronizationRuns.AsNoTracking().SingleAsync(r => r.Id == id);
          check(persisted.Status == SynchronizationStatus.Succeeded && persisted.Error == null && persisted.Updated > 0,
            "audit failure never overwrites successful synchronization state or counters");
        }
        check(failure != "stopping" || running.IsCancellationRequested, "caller cancellation inside post-commit audit preserves success");
      }
      check(delivery.Events.SequenceEqual(new[] { AuditEventType.SynchronizationSettingsChanged,
        AuditEventType.SynchronizationCompleted, AuditEventType.SynchronizationCompleted }), "each committed synchronization operation attempts audit once");
      check(delivery.Errors.Count == (failure == "none" ? 0 : 3) && delivery.Errors.All(e => e.Exception != null && e.Message.Contains("не доставлено")) &&
        (failure == "none" || ids.All(id => delivery.Errors.Count(e => e.Message.Contains(id.ToString())) == 1)),
        "post-commit audit failures log original exceptions and exact run IDs without false execution failures");
      check(failure != "stopping" || saving.IsCancellationRequested, "settings token cancellation after commit preserves success");

      api.ThrowOnFetch = true;
      before = delivery.Events.Count;
      try {
        var failed = await service.RunAsync(updatedSince: new(2000, 8, 10));
        check(failed.Status == SynchronizationStatus.Failed && failed.Error != null && failed.Updated == 0,
          "pre-commit fetch failure retains failed result and zero counters");
        check(delivery.Events.Count == before, "failed synchronization emits no successful completion event");
      }
      finally { api.ThrowOnFetch = false; }
    }
    api.Snapshot = original;
    var restored = await normalRunner.RunAsync("test-api-root", null, false, default);
    check(restored.Status == SynchronizationStatus.Succeeded, "later synchronization acquires released lock and restores baseline snapshot");
    var finalSettings = (await normalService.GetOverviewAsync()).Settings;
    await normalService.SaveSettingsAsync(DataManagementMode.Api, false, new(2, 0), finalSettings.Version);
  }

  private sealed class CompletionDelivery(string failure) : IAuditService, ILogger<ApiSynchronizationRunner>, ILogger<SynchronizationService>
  {
    public CancellationTokenSource Stopping = null!;
    public Func<AuditEventType, string?, string?, Task<bool>> Observe = null!;
    public List<AuditEventType> Events { get; } = [];
    public List<(Exception? Exception, string Message)> Errors { get; } = [];
    public bool SawCommit;
    public void LogIdentityEvent(AuditEventType type, string? user, string? ip = null, string? details = null) {
      Events.Add(type); SawCommit = Observe(type, user, details).GetAwaiter().GetResult();
      if (failure == "stopping") Stopping.Cancel();
      if (failure is "cancelled" or "stopping") throw new OperationCanceledException("Synthetic synchronization audit cancellation");
      if (failure == "io") throw new IOException("Synthetic synchronization audit failure");
    }
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
      if (logLevel >= LogLevel.Error) Errors.Add((exception, formatter(state, exception)));
    }
  }
}
