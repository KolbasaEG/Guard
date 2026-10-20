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
      using var saving = new CancellationTokenSource(); delivery.Stopping = saving;
      var previous = (await service.GetOverviewAsync()).Settings;
      var time = previous.DailyTime == new TimeOnly(3, 0) ? new TimeOnly(4, 0) : new TimeOnly(3, 0);
      delivery.Observe = async entry => {
        await using var db = await factory.CreateDbContextAsync();
        return await db.SynchronizationSettings.AnyAsync(s => s.DailyTime == time && s.Version != previous.Version);
      };
      if (failure == "none") await service.SaveSettingsAsync(DataManagementMode.Api, false, time, previous.Version, saving.Token);
      else {
        if (failure == "io") await PostgresTests.ThrowsAsync<IOException>(() => service.SaveSettingsAsync(DataManagementMode.Api, false, time, previous.Version, saving.Token), check, "settings audit I/O failure aborts synchronization settings");
        else await PostgresTests.ThrowsAsync<OperationCanceledException>(() => service.SaveSettingsAsync(DataManagementMode.Api, false, time, previous.Version, saving.Token), check, "settings audit cancellation aborts synchronization settings");
        check((await service.GetOverviewAsync()).Settings.Version == previous.Version && !delivery.SawCommit,
          "failed audit leaves synchronization settings and version unchanged");
      }
      check(!delivery.SawCommit, "synchronization settings audit runs before commit");
      var saved = (await service.GetOverviewAsync()).Settings;
      if (failure == "none") {
        check(saved.Version != previous.Version && saved.DailyTime == time, "audited settings are committed");
        var before = delivery.Events.Count;
        await PostgresTests.ThrowsAsync<EntityConflictException>(() => service.SaveSettingsAsync(DataManagementMode.Api, false, time, previous.Version), check, "stale synchronization settings remain rejected");
        check(delivery.Events.Count == before, "stale synchronization settings produce no completion event");
      }
      var trusted = permissions.Current; permissions.Current = new("non-root", false, []);
      try {
        await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveSettingsAsync(DataManagementMode.Local, false, time, saved.Version), check, "non-Root settings command forbidden");
        await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.RunAsync(), check, "non-Root synchronization forbidden");
      } finally { permissions.Current = trusted; }
      check((await service.GetOverviewAsync()).Settings.Version == saved.Version, "access rejection preserves settings");

      foreach (var queued in new[] { false, true }) {
        using var running = new CancellationTokenSource(); delivery.Stopping = running;
        var changed = JsonSerializer.Deserialize<ApiPersonal>(JsonSerializer.Serialize(original.Personnel[0]))!;
        changed.FirstName = $"Completion {failure} {queued}";
        api.Snapshot = new ApiSnapshot([], [], [changed]);
        string? beforeName;
        await using (var db = await factory.CreateDbContextAsync()) beforeName = await db.Personals.Where(p => p.ExternalId == changed.Id).Select(p => p.FirstName).SingleAsync();
        delivery.Observe = async entry => {
          await using var db = await factory.CreateDbContextAsync();
          return await db.SynchronizationRuns.AnyAsync(r => r.Id == Guid.Parse(entry.ObjectId!) && r.Status == SynchronizationStatus.Succeeded) ||
            await db.Personals.AnyAsync(p => p.ExternalId == changed.Id && p.FirstName == changed.FirstName);
        };
        SynchronizationRunDto result;
        if (queued) {
          var request = await service.RunAsync(updatedSince: new(2000, 8, 10), enqueue: true);
          var run = await runner.RunAsync("untrusted-argument", null, false, running.Token, queuedRunId: request.Id);
          result = SynchronizationProjection.FromRun(run);
          check(result.Id == request.Id && run.ActorId == "test-api-root", "queued synchronization uses persisted trusted author");
        } else result = await service.RunAsync(running.Token, new(2000, 8, 10));
        check(!delivery.SawCommit, "synchronization result audit precedes data commit");
        await using var verify = await factory.CreateDbContextAsync();
        var persisted = await verify.SynchronizationRuns.SingleAsync(r => r.Id == result.Id);
        var name = await verify.Personals.Where(p => p.ExternalId == changed.Id).Select(p => p.FirstName).SingleAsync();
        if (failure == "none") check(persisted.Status == SynchronizationStatus.Succeeded && persisted.Updated > 0 && name == changed.FirstName,
          "successful synchronization commits data and result");
        else check(persisted.Status is SynchronizationStatus.Failed or SynchronizationStatus.Cancelled && persisted.Updated == 0 && name == beforeName,
          "failed mandatory completion audit rolls back imported data and successful counters");
        check(failure != "stopping" || running.IsCancellationRequested, "caller cancellation is exercised before synchronization commit");
      }
      check(delivery.Events.Count(e => e.EventType == AuditEventType.SynchronizationCompleted) == 2 &&
        delivery.Events.All(e => e.Actor == "test-api-root"), "each synchronization attempts one completion with trusted author");
      api.ThrowOnFetch = true;
      var count = delivery.Events.Count;
      try {
        var failed = await service.RunAsync(updatedSince: new(2000, 8, 10));
        check(failed.Status == SynchronizationStatus.Failed && failed.Error != null && failed.Updated == 0 && delivery.Events.Count == count,
          "fetch failure changes no data and emits no success event");
      } finally { api.ThrowOnFetch = false; }
    }
    api.Snapshot = original;
    check((await normalRunner.RunAsync("test-api-root", null, false, default)).Status == SynchronizationStatus.Succeeded,
      "next synchronization obtains released lock and restores baseline");
    var final = (await normalService.GetOverviewAsync()).Settings;
    await normalService.SaveSettingsAsync(DataManagementMode.Api, false, new(2, 0), final.Version);
  }

  private sealed class CompletionDelivery(string failure) : IAuditService, ILogger<ApiSynchronizationRunner>, ILogger<SynchronizationService>
  {
    public CancellationTokenSource Stopping = null!;
    public Func<AuditEventData, Task<bool>> Observe = null!;
    public List<AuditEventData> Events { get; } = [];
    public bool SawCommit;
    public async Task RecordAsync(AuditEventData entry, CancellationToken ct = default) {
      ct.ThrowIfCancellationRequested(); Events.Add(entry); SawCommit = await Observe(entry);
      if (failure == "stopping") Stopping.Cancel();
      if (failure is "cancelled" or "stopping") throw new OperationCanceledException("Synthetic synchronization audit cancellation");
      if (failure == "io") throw new IOException("Synthetic synchronization audit failure");
    }
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
  }
}
