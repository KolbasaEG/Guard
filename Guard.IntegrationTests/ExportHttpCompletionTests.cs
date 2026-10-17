using Guard.Web.Exports;
using System.Net;
using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal static partial class ExportIntegrationTests
{
  private static async Task CheckHttpAuditCompletionAsync(IDbContextFactory<ApplicationDbContext> factory,
    IAccountPolicyService policies, ExportStorage storage, IOptions<ExportOptions> options, DataExportService service,
    ExportProcessor processor, ApplicationUser user, ApplicationUser root, Action<bool, string> check)
  {
    var delivery = new HttpAuditDelivery(factory);
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
    builder.Logging.ClearProviders(); builder.Logging.AddProvider(delivery); builder.WebHost.UseUrls("http://127.0.0.1:0");
    builder.Services.AddSingleton(factory); builder.Services.AddSingleton(policies); builder.Services.AddSingleton(storage);
    builder.Services.AddSingleton(options); builder.Services.AddSingleton<IAuditService>(delivery); builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<AuthenticationStateProvider, HttpAuthentication>();
    builder.Services.AddScoped<ICurrentSession, Guard.Components.Account.CurrentSessionAdapter>();
    builder.Services.AddScoped<IPermissionReader, PermissionReader>();
    builder.Services.AddScoped<IPermissionService, PermissionService>(); builder.Services.AddScoped<ExportAccess>(); builder.Services.AddScoped<IExportDeliveryService, ExportDeliveryService>();
    builder.Services.AddAuthentication("export-test").AddScheme<AuthenticationSchemeOptions, TestHandler>("export-test", _ => { });
    builder.Services.AddAuthorization(); builder.Services.AddAntiforgery();
    await using var app = builder.Build();
    app.Use(async (ctx, next) => {
      if (!ctx.Request.Path.StartsWithSegments("/exports")) { await next(); return; }
      if (ctx.Request.Headers.ContainsKey("X-Test-Broken-Body")) ctx.Response.Body = new BrokenDeliveryBody(ctx.Response.Body);
      try { await next(); } finally { delivery.RequestDone.TrySetResult(); }
    });
    app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery();
    app.MapGet("/csrf", (HttpContext ctx, IAntiforgery anti) => Results.Json(anti.GetAndStoreTokens(ctx)));
    app.MapExportEndpoints(); await app.StartAsync();
    try {
      var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
      using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
      using var client = new HttpClient(handler) { BaseAddress = new Uri(url) };
      client.DefaultRequestHeaders.Add("X-Test-User", user.Id);
      var tokens = JsonSerializer.Deserialize<AntiforgeryTokenSet>(await client.GetStringAsync("/csrf"), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
      async Task<HttpResponseMessage> Send(Guid id, bool csrf = true, string? header = null) {
        delivery.RequestDone = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/exports/{id}/download");
        request.Content = csrf ? new FormUrlEncodedContent([new(tokens.FormFieldName, tokens.RequestToken!)]) : new StringContent("");
        if (header != null) request.Headers.Add(header, header == "Range" ? "bytes=0-1" : "true");
        var response = await client.SendAsync(request);
        await delivery.RequestDone.Task.WaitAsync(TimeSpan.FromSeconds(10));
        return response;
      }
      foreach (var failure in new[] { "none", "io", "cancelled" }) {
        var id = await service.CreateAsync(new(ExportArea.Personals, ExportFormat.Excel, IncludeChildren: true));
        await processor.RunNextAsync(default); delivery.Reset(failure);
        await using (var db = await factory.CreateDbContextAsync())
          await db.ExportJobs.Where(j => j.Id == id).ExecuteUpdateAsync(u => u.SetProperty(j => j.ClientIp, "198.18.200.1"));
        using var response = await Send(id);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        check(response.StatusCode == HttpStatusCode.OK && response.Content.Headers.ContentDisposition?.DispositionType == "attachment" &&
          response.Headers.CacheControl?.NoStore == true, "audit failure preserves HTTP attachment and cache protection");
        await using (var db = await factory.CreateDbContextAsync()) {
          var job = await db.ExportJobs.AsNoTracking().SingleAsync(j => j.Id == id);
          var artifact = await db.ExportArtifacts.AsNoTracking().SingleAsync(a => a.JobId == id);
          var attempt = await db.ExportDeliveryAttempts.AsNoTracking().SingleAsync(a => a.JobId == id);
          check(job.State == ExportJobState.Delivered && job.Error == null && attempt.Succeeded && attempt.CompletedAtUtc != null,
            "audit failure preserves committed successful delivery and single consumed attempt");
          check(bytes.LongLength == artifact.Size && Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)) == artifact.Sha256 &&
            File.Exists(storage.ArtifactPath(artifact.StorageKey)), "HTTP bytes match retained exact archive after audit failure");
        }
        check(delivery.Events.Select(e => e.Type).SequenceEqual([AuditEventType.ExportDeliveryStarted, AuditEventType.ExportDeliveryCompleted]) &&
          delivery.Events.All(e => e.Author == user.Id && e.Details == $"Export {id}" && e.SawCommit) &&
          delivery.Events[0].Ip == "198.18.200.1" && delivery.Events[1].Ip == null,
          "HTTP audit sees committed claim and final result in independent contexts with original actor and job");
        check(delivery.Errors.Count == (failure == "none" ? 0 : 2) && delivery.Errors.All(e =>
          e.Exception is IOException or OperationCanceledException && e.Message.Contains(id.ToString()) && e.Message.Contains("не доставлено")) &&
          (failure == "none" || delivery.Errors.Any(e => e.Message.Contains("ExportDeliveryStarted")) && delivery.Errors.Any(e => e.Message.Contains("ExportDeliveryCompleted"))),
          "each failed delivery event logs original exception, event type and job without false transfer failure");
        using var repeat = await Send(id);
        check(repeat.StatusCode == HttpStatusCode.Conflict, "audit failure does not reopen one-time download and archive lock is released");
        await using var verify = await factory.CreateDbContextAsync();
        check(await verify.ExportDeliveryAttempts.CountAsync(a => a.JobId == id) == 1 &&
          await verify.ExportJobs.AnyAsync(j => j.Id == id && j.State == ExportJobState.Delivered), "repeat request never changes confirmed delivery");
      }

      var control = await service.CreateAsync(new(ExportArea.Personals, ExportFormat.Word));
      await processor.RunNextAsync(default); delivery.Reset("io");
      using (var noToken = await Send(control, csrf: false)) check(noToken.StatusCode == HttpStatusCode.Forbidden, "audit outage preserves CSRF denial");
      using (var range = await Send(control, header: "Range")) check(range.StatusCode == HttpStatusCode.Conflict, "audit outage preserves Range denial");
      client.DefaultRequestHeaders.Remove("X-Test-User"); client.DefaultRequestHeaders.Add("X-Test-User", root.Id);
      tokens = JsonSerializer.Deserialize<AntiforgeryTokenSet>(await client.GetStringAsync("/csrf"), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
      using (var foreign = await Send(control)) check(foreign.StatusCode == HttpStatusCode.NotFound, "audit outage does not let Root download another author's file");
      client.DefaultRequestHeaders.Remove("X-Test-User"); client.DefaultRequestHeaders.Add("X-Test-User", user.Id);
      tokens = JsonSerializer.Deserialize<AntiforgeryTokenSet>(await client.GetStringAsync("/csrf"), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
      string originalHash;
      await using (var db = await factory.CreateDbContextAsync()) {
        originalHash = await db.ExportArtifacts.Where(a => a.JobId == control).Select(a => a.Sha256).SingleAsync();
        await db.ExportArtifacts.Where(a => a.JobId == control).ExecuteUpdateAsync(u => u.SetProperty(a => a.Sha256, new string('0', 64)));
      }
      using (var corrupt = await Send(control)) check(corrupt.StatusCode == HttpStatusCode.Conflict &&
        corrupt.Content.Headers.ContentDisposition == null, "audit outage never bypasses archive hash verification");
      await using (var db = await factory.CreateDbContextAsync())
        await db.ExportArtifacts.Where(a => a.JobId == control).ExecuteUpdateAsync(u => u.SetProperty(a => a.Sha256, originalHash));
      await using (var db = await factory.CreateDbContextAsync())
        check(await db.ExportJobs.AnyAsync(j => j.Id == control && j.State == ExportJobState.Ready) &&
          !await db.ExportDeliveryAttempts.AnyAsync(a => a.JobId == control), "pre-claim denials leave Ready and create no delivery attempt");

      delivery.Reset("cancelled");
      using (var broken = await Send(control, header: "X-Test-Broken-Body"))
        check(broken.StatusCode == HttpStatusCode.Conflict && !(await broken.Content.ReadAsStringAsync()).Contains("Synthetic"),
          "real write failure still returns safe HTTP error despite cancelled audit delivery");
      await using (var db = await factory.CreateDbContextAsync()) {
        var job = await db.ExportJobs.AsNoTracking().SingleAsync(j => j.Id == control);
        var attempt = await db.ExportDeliveryAttempts.AsNoTracking().SingleAsync(a => a.JobId == control);
        check(job.State == ExportJobState.Failed && job.Error != null && !attempt.Succeeded && attempt.CompletedAtUtc != null,
          "real body failure consumes attempt and commits Failed even when failure audit throws");
      }
      check(delivery.Events.Select(e => e.Type).SequenceEqual([AuditEventType.ExportDeliveryStarted, AuditEventType.ExportFailed, AuditEventType.ExportFailed]) &&
        delivery.Errors.Count == 3 && delivery.Events.First().SawCommit && delivery.Events.Last().SawCommit,
        "real write failure retains existing failure events and independently logs each audit outage");
      using var retry = await Send(control);
      check(retry.StatusCode == HttpStatusCode.Conflict, "failed body transfer cannot be retried with consumed one-time link");
    }
    finally { await app.StopAsync(); }
  }

  private sealed class HttpAuditDelivery(IDbContextFactory<ApplicationDbContext> factory) : IAuditService, ILoggerProvider, ILogger
  {
    private string failure = "none";
    public TaskCompletionSource RequestDone = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public List<(AuditEventType Type, string? Author, string? Details, string? Ip, bool SawCommit)> Events { get; } = [];
    public List<(Exception? Exception, string Message)> Errors { get; } = [];
    public void Reset(string mode) { failure = mode; Events.Clear(); Errors.Clear(); }
    public void LogIdentityEvent(AuditEventType type, string? user, string? ip = null, string? details = null) {
      bool committed = false;
      if (details?.StartsWith("Export ") == true && Guid.TryParse(details[7..], out var id)) {
        using var db = factory.CreateDbContext();
        var job = db.ExportJobs.AsNoTracking().SingleOrDefault(j => j.Id == id);
        var attempt = db.ExportDeliveryAttempts.AsNoTracking().SingleOrDefault(a => a.JobId == id);
        committed = type == AuditEventType.ExportDeliveryStarted ? job?.State == ExportJobState.Delivering && attempt?.CompletedAtUtc == null && attempt != null :
          type == AuditEventType.ExportDeliveryCompleted ? job?.State == ExportJobState.Delivered && attempt is { Succeeded: true, CompletedAtUtc: not null } :
          job?.State == ExportJobState.Failed && attempt is { Succeeded: false, CompletedAtUtc: not null };
      }
      Events.Add((type, user, details, ip, committed));
      if (failure == "io") throw new IOException("Synthetic HTTP audit outage");
      if (failure == "cancelled") throw new OperationCanceledException("Synthetic HTTP audit cancellation");
    }
    public ILogger CreateLogger(string categoryName) => categoryName == "Guard.Core.Services.Exports.Delivery" ? this : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    public void Dispose() { }
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel level) => true;
    public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
      if (level >= LogLevel.Error) Errors.Add((exception, formatter(state, exception)));
    }
  }

  private sealed class BrokenDeliveryBody(Stream original) : Stream
  {
    private bool failed;
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => original.Flush();
    public override Task FlushAsync(CancellationToken ct) => original.FlushAsync(ct);
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) { FailOnce(); original.Write(buffer, offset, count); }
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) { FailOnce(); return original.WriteAsync(buffer, offset, count, ct); }
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) { FailOnce(); return original.WriteAsync(buffer, ct); }
    private void FailOnce() { if (!failed) { failed = true; throw new IOException("Synthetic HTTP body failure"); } }
  }
}
