using Guard.Core.Contexts;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal static class ExportCommandCompletionTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, IPermissionService permissions,
      ExportAccess access, ExportStorage storage, IOptions<ExportOptions> options, IHttpContextAccessor http,
      Action<string> setUser, string user, string root, Func<Task> cleanup, Action<bool, string> check)
  {
    foreach (var failure in new[] { "none", "io", "cancelled", "stopping" }) {
      var expectedIp = failure switch { "none" => "198.18.200.1", "io" => "2001:db8::1", _ => null };
      http.HttpContext = failure == "stopping" ? null : new DefaultHttpContext();
      if (http.HttpContext is { } context) {
        context.Connection.RemoteIpAddress = failure switch {
          "none" => System.Net.IPAddress.Parse("::ffff:198.18.200.1"),
          "io" => System.Net.IPAddress.Parse("2001:db8::1"), _ => null };
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.99";
      }
      var delivery = new Delivery(failure);
      var service = new DataExportService(factory, permissions, access, storage, options, delivery, new Guard.Web.Identity.ClientIpAccessor(http), delivery);
      setUser(user);
      using var creating = new CancellationTokenSource();
      delivery.Stopping = creating;
      delivery.Observe = async (type, actor, details) => {
        var id = Guid.Parse(details!.Split(';')[0][7..]);
        await using var db = await factory.CreateDbContextAsync();
        var job = await db.ExportJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id);
        return type == AuditEventType.ExportRequested && actor == user && job != null && job.AuthorId == user &&
          job.ClientIp == expectedIp && job.State == ExportJobState.Queued;
      };
      if (failure != "none") {
        await using var absent = await factory.CreateDbContextAsync(); var previousCount = await absent.ExportJobs.CountAsync();
        if (failure == "io") await PostgresTests.ThrowsAsync<IOException>(() => service.CreateAsync(new(ExportArea.Personals, ExportFormat.Excel), creating.Token), check, "failed mandatory audit rejects export creation");
        else await PostgresTests.ThrowsAsync<OperationCanceledException>(() => service.CreateAsync(new(ExportArea.Personals, ExportFormat.Excel), creating.Token), check, "cancelled mandatory audit rejects export creation");
        check(await absent.ExportJobs.CountAsync() == previousCount && !delivery.SawCommit && delivery.Events.Count == 1,
          "export request and mandatory audit roll back together before a job becomes visible");
        await cleanup(); continue;
      }
      var id = await service.CreateAsync(new(ExportArea.Personals, ExportFormat.Excel), creating.Token);
      check(!delivery.SawCommit, "export request audit precedes visibility of the committed job");
      check(delivery.LastIp == expectedIp, "export request audit retains trusted normalized client IP or null without HTTP address");
      await using (var db = await factory.CreateDbContextAsync()) {
        check(await db.ExportJobs.CountAsync(j => j.Id == id && j.AuthorId == user && !j.CancelRequested) == 1,
          "export create returns saved ID despite audit delivery failure");
      }

      setUser(root);
      await PostgresTests.ThrowsAsync<EntityRuleException>(() => service.CancelAsync(id), check,
        "another author cannot cancel an export even with Root access");
      check(delivery.Events.Count == 1, "foreign cancellation delivers no completion event");
      setUser(user);

      using var cancelling = new CancellationTokenSource();
      delivery.Stopping = cancelling;
      delivery.Observe = async (type, actor, details) => {
        await using var db = await factory.CreateDbContextAsync();
        return type == AuditEventType.ExportCancelled && actor == user && details == $"Export {id}" &&
          await db.ExportJobs.AnyAsync(j => j.Id == id && j.CancelRequested);
      };
      await service.CancelAsync(id, cancelling.Token);
      check(!delivery.SawCommit, "cancel audit precedes committed cancellation and retains exact target");
      await using (var db = await factory.CreateDbContextAsync()) {
        check(await db.ExportJobs.AnyAsync(j => j.Id == id && j.CancelRequested), "cancel succeeds despite audit delivery failure");
      }

      var beforeRejected = delivery.Events.Count;
      await PostgresTests.ThrowsAsync<EntityRuleException>(() => service.CancelAsync(Guid.NewGuid()), check,
        "missing export cancellation still fails before completion");
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveRetentionAsync(4, Guid.Empty), check,
        "non-Root retention write remains forbidden");
      check(delivery.Events.Count == beforeRejected, "rejected commands deliver no success audit");

      setUser(root);
      var previous = await service.GetRetentionAsync();
      var years = previous.Years == 4 ? 3 : 4;
      using var configuring = new CancellationTokenSource();
      delivery.Stopping = configuring;
      delivery.Observe = async (type, actor, details) => {
        await using var db = await factory.CreateDbContextAsync();
        return type == AuditEventType.ExportRetentionChanged && actor == root && details == $"Years {years}" &&
          await db.ExportRetentionSettings.AnyAsync(s => s.Id == 1 && s.Years == years && s.Version != previous.Version && s.ModifiedBy == root);
      };
      await service.SaveRetentionAsync(years, previous.Version, configuring.Token);
      check(!delivery.SawCommit, "retention audit precedes commit of the new version and trusted Root");
      var saved = await service.GetRetentionAsync();
      check(saved.Years == years && saved.Version != previous.Version, "retention command returns success after confirmed write");
      beforeRejected = delivery.Events.Count;
      await PostgresTests.ThrowsAsync<DbUpdateConcurrencyException>(() => service.SaveRetentionAsync(5, previous.Version), check,
        "stale retention version still fails before completion");
      await PostgresTests.ThrowsAsync<ArgumentException>(() => service.SaveRetentionAsync(31, saved.Version), check,
        "invalid retention still fails before completion");
      check(delivery.Events.Count == beforeRejected && (await service.GetRetentionAsync()) == saved,
        "rejected retention commands change no data and deliver no audit");
      check(delivery.Events.SequenceEqual(new[] { AuditEventType.ExportRequested, AuditEventType.ExportCancelled, AuditEventType.ExportRetentionChanged }),
        "each committed export command attempts its own audit exactly once");
      check(delivery.Errors.Count == (failure == "none" ? 0 : 3) && delivery.Errors.All(e => e.Exception != null) &&
        (failure == "none" || (delivery.Errors.Count(e => e.Message.Contains(id.ToString())) == 2 &&
          delivery.Errors.All(e => e.Message.Contains("не доставлено")))), "audit failures log original exceptions and committed targets");
      check(failure != "stopping" || (creating.IsCancellationRequested && cancelling.IsCancellationRequested && configuring.IsCancellationRequested),
        "cancellation after persistence does not turn committed commands into failure");
      await cleanup();
    }
    setUser(user);
    http.HttpContext = null;
    await using var before = await factory.CreateDbContextAsync();
    var count = await before.ExportJobs.CountAsync();
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    var cancelledService = new DataExportService(factory, permissions, access, storage, options, new AuditStub(),
      new Guard.Web.Identity.ClientIpAccessor(http), Microsoft.Extensions.Logging.Abstractions.NullLogger<DataExportService>.Instance);
    await PostgresTests.ThrowsAsync<OperationCanceledException>(() => cancelledService.CreateAsync(
      new(ExportArea.Personals, ExportFormat.Excel), cancelled.Token), check, "cancelled export creation propagates cancellation");
    check(await before.ExportJobs.CountAsync() == count, "cancelled export creation stores no job");
  }

  private sealed class Delivery(string failure) : IAuditService, ILogger<DataExportService>
  {
    public CancellationTokenSource Stopping = null!;
    public Func<AuditEventType, string?, string?, Task<bool>> Observe = null!;
    public List<AuditEventType> Events { get; } = [];
    public List<(Exception? Exception, string Message)> Errors { get; } = [];
    public bool SawCommit;
    public string? LastIp;
    public Task RecordAsync(Guard.Core.Services.AuditEventData entry, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); LogIdentityEvent(entry.EventType, entry.Actor, entry.ClientIp, entry.Details); return Task.CompletedTask; }
    public void LogIdentityEvent(AuditEventType type, string? user, string? ip = null, string? details = null) {
      LastIp = ip;
      Events.Add(type);
      SawCommit = Observe(type, user, details).GetAwaiter().GetResult();
      if (failure == "stopping") Stopping.Cancel();
      if (failure is "cancelled" or "stopping") throw new OperationCanceledException("Synthetic command audit cancellation");
      if (failure == "io") throw new IOException("Synthetic command audit failure");
    }
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
      if (logLevel >= LogLevel.Error) Errors.Add((exception, formatter(state, exception)));
    }
  }
}
