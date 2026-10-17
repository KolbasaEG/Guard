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
      check(await processor.RunNextAsync(stopping.Token), $"export reports processed job after {failure} audit delivery");
      check(delivery.Events.SequenceEqual([AuditEventType.ExportArchived]) && delivery.SawCommit,
        "audit sees committed Ready and artifact; failed delivery emits no false ExportFailed event");
      check(delivery.Actor == author && delivery.Details?.Contains($"Export {id}; rows 2; SHA256 ") == true,
        "archival audit retains job, actor, row count and hash");
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
      check(!await processor.RunNextAsync(default) && delivery.Events.Count == 1, "Ready job is not generated or audited again on next queue run");
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
    check(failedDelivery.Events.SequenceEqual([AuditEventType.ExportFailed]) && failedDelivery.Errors.Single().Exception is InvalidDataException,
      "pre-commit failure retains failure audit and original diagnostic exception");
  }

  private sealed class Delivery(string failure, CancellationTokenSource stopping) : IAuditService, ILogger<ExportProcessor>
  {
    public Func<Task<bool>> Observe = () => Task.FromResult(false);
    public List<AuditEventType> Events { get; } = [];
    public List<(Exception? Exception, string Message)> Errors { get; } = [];
    public bool SawCommit;
    public string? Actor, Details;
    public void LogIdentityEvent(AuditEventType type, string? user, string? ip = null, string? details = null) {
      Events.Add(type); Actor = user; Details = details;
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
