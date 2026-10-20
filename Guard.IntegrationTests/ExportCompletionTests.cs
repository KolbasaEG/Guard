using Guard.Core.Contexts;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal static class ExportCompletionTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, DataExportService service,
      ExportAccess access, ExportStorage storage, IOptions<ExportOptions> options, string author, Action<bool, string> check)
  {
    foreach (var failure in new[] { "none", "io", "cancelled", "stopping" }) {
      using var stopping = new CancellationTokenSource();
      var id = await service.CreateAsync(new(ExportArea.Personals, ExportFormat.Excel, IncludeChildren: true));
      var delivery = new Delivery(failure, stopping);
      delivery.Observe = async () => {
        await using var db = await factory.CreateDbContextAsync();
        var job = await db.ExportJobs.AsNoTracking().SingleAsync(j => j.Id == id);
        var artifact = await db.ExportArtifacts.AsNoTracking().SingleOrDefaultAsync(a => a.JobId == id);
        return job.State == ExportJobState.Ready && job.RowCount == 2 && job.CompletedAtUtc != null &&
          artifact != null && File.Exists(storage.ArtifactPath(artifact.StorageKey));
      };
      var processor = new ExportProcessor(factory, access, new ExportSnapshot(factory, storage),
        new ExportDocumentWriter(storage, options), storage, delivery, options, delivery);
      if (failure == "stopping") await PostgresTests.ThrowsAsync<OperationCanceledException>(() => processor.RunNextAsync(stopping.Token), check,
        "host cancellation before mandatory archive audit propagates without claiming Ready");
      else check(await processor.RunNextAsync(stopping.Token), $"export processes the job with {failure} mandatory audit");
      if (failure != "none") {
        await using var absent = await factory.CreateDbContextAsync();
        check(await absent.ExportJobs.AnyAsync(j => j.Id == id && j.State != ExportJobState.Ready) &&
          !await absent.ExportArtifacts.AnyAsync(a => a.JobId == id) && !delivery.SawCommit,
          "failed archive audit prevents Ready and leaves no committed artifact");
        await processor.CleanupAsync(default); continue;
      }
      check(delivery.Events.SequenceEqual([AuditEventType.ExportProcessingStarted, AuditEventType.ExportArchived]) && !delivery.SawCommit,
        "processing and archive events precede commit of Ready and its artifact");
      check(delivery.Actor == null && delivery.Details?.Contains($"RequestedBy={author}; rows=2;") == true,
        "archival audit identifies the system initiator, requesting account, row count and hash");
      check(delivery.Errors.Count == (failure == "none" ? 0 : 1) && delivery.Errors.All(e => e.Exception != null &&
        e.Message.Contains(id.ToString()) && !e.Message.Contains("завершилось ошибкой")), "audit failure logs delivery error with job ID");
      await using (var db = await factory.CreateDbContextAsync()) {
        var job = await db.ExportJobs.AsNoTracking().SingleAsync(j => j.Id == id);
        var artifact = await db.ExportArtifacts.AsNoTracking().SingleAsync(a => a.JobId == id);
        check(job.State == ExportJobState.Ready && job.Error == null && job.RowCount == 2 && job.CompletedAtUtc != null,
          "committed export remains Ready without generation error after audit failure");
        await storage.VerifyAsync(artifact.StorageKey, artifact.Sha256, artifact.Size, default);
        check(artifact.Size > 0 && delivery.Details?.EndsWith(artifact.Sha256) == true && !storage.WorkIds().Contains(id),
          "exact archive passes hash verification, audit hash matches metadata and work files are removed");
      }
      check(!await processor.RunNextAsync(default) && delivery.Events.Count == 2, "Ready job is not generated or audited again on next queue run");
      await service.CancelAsync(id);
      await processor.CleanupAsync(default);
    }

    // A genuine pre-commit failure must retain the existing failure path.
    var invalidId = await service.CreateAsync(new(ExportArea.Personals, ExportFormat.Word));
    await using (var db = await factory.CreateDbContextAsync())
      await db.ExportJobs.Where(j => j.Id == invalidId).ExecuteUpdateAsync(u => u.SetProperty(j => j.SchemaVersion, 999));
    using var active = new CancellationTokenSource();
    var failedDelivery = new Delivery("none", active);
    var failingProcessor = new ExportProcessor(factory, access, new ExportSnapshot(factory, storage),
      new ExportDocumentWriter(storage, options), storage, failedDelivery, options, failedDelivery);
    check(await failingProcessor.RunNextAsync(default), "invalid export schema is handled as generation failure");
    await using (var db = await factory.CreateDbContextAsync()) {
      var failed = await db.ExportJobs.AsNoTracking().SingleAsync(j => j.Id == invalidId);
      check(failed.State == ExportJobState.Failed && failed.Error != null && !await db.ExportArtifacts.AnyAsync(a => a.JobId == invalidId),
        "pre-commit failure creates no archive metadata and persists Failed");
    }
    check(failedDelivery.Events.SequenceEqual([AuditEventType.ExportProcessingStarted, AuditEventType.ExportFailed]) && failedDelivery.Errors.Single().Exception is InvalidDataException,
      "pre-commit failure retains failure audit and original diagnostic exception");
    var unknownId = await service.CreateAsync(new(ExportArea.Personals, ExportFormat.Excel));
    await using (var db = await factory.CreateDbContextAsync()) {
      await db.ExportJobs.Where(j => j.Id == unknownId).ExecuteUpdateAsync(s => s.SetProperty(j => j.State, ExportJobState.Delivering));
      db.ExportDeliveryAttempts.Add(new() { JobId = unknownId, StartedAtUtc = DateTimeOffset.UtcNow }); await db.SaveChangesAsync();
    }
    var recoveryFault = new Delivery("recovery", active);
    var recoveryProcessor = new ExportProcessor(factory, access, new ExportSnapshot(factory, storage),
      new ExportDocumentWriter(storage, options), storage, recoveryFault, options, recoveryFault);
    await PostgresTests.ThrowsAsync<IOException>(() => recoveryProcessor.RecoverAsync(default), check,
      "restart recovery cannot change delivery result without its mandatory event");
    await using (var db = await factory.CreateDbContextAsync())
      check(await db.ExportJobs.AnyAsync(j => j.Id == unknownId && j.State == ExportJobState.Delivering) &&
        await db.ExportDeliveryAttempts.AnyAsync(a => a.JobId == unknownId && a.CompletedAtUtc == null), "failed recovery audit rolls back status and attempt together");
    await failingProcessor.RecoverAsync(default);
    await using (var db = await factory.CreateDbContextAsync())
      check(await db.ExportJobs.AnyAsync(j => j.Id == unknownId && j.Error!.Contains("неизвестен")) &&
        await db.ExportDeliveryAttempts.AnyAsync(a => a.JobId == unknownId && a.CompletedAtUtc != null && a.Succeeded == null),
        "restart recovery explicitly preserves unknown transfer result rather than fabricating failure");
    check(failedDelivery.Records.Any(e => e.EventType == AuditEventType.ExportRecoveryCompleted && e.ObjectId == unknownId.ToString() &&
      e.Outcome == "unknown" && e.Actor == null && e.ActorKind == "system"), "recovery audit identifies unknown delivery and system initiator");
    foreach (var recovered in failedDelivery.Records.Where(e => e.ReasonCode == "GenerationRequeuedAfterRestart"))
      await service.CancelAsync(Guid.Parse(recovered.ObjectId!));
    await failingProcessor.CleanupAsync(default);
  }

  private sealed class Delivery(string failure, CancellationTokenSource stopping) : IAuditService, ILogger<ExportProcessor>
  {
    public Func<Task<bool>> Observe = () => Task.FromResult(false);
    public List<AuditEventType> Events { get; } = [];
    public List<AuditEventData> Records { get; } = [];
    public List<(Exception? Exception, string Message)> Errors { get; } = [];
    public bool SawCommit;
    public string? Actor, Details;
    public Task RecordAsync(Guard.Core.Services.AuditEventData entry, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); Records.Add(entry); LogIdentityEvent(entry.EventType, entry.Actor, entry.ClientIp, entry.Details); return Task.CompletedTask; }
    public void LogIdentityEvent(AuditEventType type, string? user, string? ip = null, string? details = null) {
      Events.Add(type); Actor = user; Details = details;
      if (failure == "recovery" && type == AuditEventType.ExportRecoveryCompleted) throw new IOException("Synthetic recovery audit failure");
      if (type != AuditEventType.ExportArchived) return;
      SawCommit = Observe().GetAwaiter().GetResult();
      if (failure == "stopping") stopping.Cancel();
      if (failure is "cancelled" or "stopping") throw new OperationCanceledException("Synthetic audit cancellation after Ready");
      if (failure == "io") throw new IOException("Synthetic audit failure after Ready");
    }
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
      if (logLevel >= LogLevel.Error) Errors.Add((exception, formatter(state, exception)));
    }
  }
}
