using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Web.Logging;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

internal static class TechnicalLogHttpTests
{
  public static async Task RunAsync(ITechnicalLogService service, TechnicalLoggingTests.AuditCapture audit, Action<bool, string> check)
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
    builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
    builder.Services.AddSingleton(service); builder.Services.AddSingleton<IAuditService>(audit);
    var endpointAccess = new TechnicalLoggingTests.Access();
    builder.Services.AddSingleton<IPermissionService>(endpointAccess);
    builder.Services.AddSingleton<IOperationAvailability>(new TechnicalLoggingTests.Available());
    builder.Services.AddAuthentication("technical-test").AddScheme<AuthenticationSchemeOptions, Handler>("technical-test", _ => { });
    builder.Services.AddAuthorization(options => options.AddPolicy(Permissions.Logs.Read, p => p.RequireAuthenticatedUser().RequireClaim("root", "true")));
    builder.Services.AddAntiforgery();
    await using var app = builder.Build();
    var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    app.Use(async (http, next) => { try { await next(); } finally { if (http.Request.Path == "/technical-logs/export") done.TrySetResult(); } });
    app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery();
    app.MapGet("/csrf", (HttpContext http, IAntiforgery anti) => Results.Json(anti.GetAndStoreTokens(http)));
    app.MapTechnicalLogEndpoints(); await app.StartAsync();
    try {
      using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
      using var client = new HttpClient(handler) { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
      async Task<HttpResponseMessage> Send(AntiforgeryTokenSet? token, bool range = false, string? source = null) {
        done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/technical-logs/export");
        var values = new Dictionary<string, string> { ["fromUtc"] = DateTime.UtcNow.AddHours(-1).ToString("O"), ["toUtc"] = DateTime.UtcNow.AddMinutes(1).ToString("O") };
        if (source != null) values["source"] = source;
        if (token != null) values[token.FormFieldName] = token.RequestToken!;
        request.Content = new FormUrlEncodedContent(values);
        if (range) request.Headers.Add("Range", "bytes=0-1");
        var response = await client.SendAsync(request); await done.Task.WaitAsync(TimeSpan.FromSeconds(10)); return response;
      }
      using (var anonymous = await Send(null)) check(anonymous.StatusCode == HttpStatusCode.Unauthorized && anonymous.Content.Headers.ContentDisposition == null, "technical export rejects anonymous HTTP requests");
      client.DefaultRequestHeaders.Add("X-Test-Actor", "user");
      using (var ordinary = await Send(null)) check(ordinary.StatusCode == HttpStatusCode.Forbidden, "technical export endpoint rejects authenticated ordinary users");
      client.DefaultRequestHeaders.Remove("X-Test-Actor"); client.DefaultRequestHeaders.Add("X-Test-Actor", "root");
      var token = JsonSerializer.Deserialize<AntiforgeryTokenSet>(await client.GetStringAsync("/csrf"), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
      var before = audit.Events.Count;
      using (var csrf = await Send(null)) check(csrf.StatusCode == HttpStatusCode.Forbidden && audit.Events.Count == before, "missing CSRF token prevents technical export generation");
      using (var range = await Send(token, true)) check(range.StatusCode == HttpStatusCode.Conflict && range.Content.Headers.ContentDisposition == null, "range request cannot obtain a partial diagnostic attachment");
      using (var response = await Send(token)) {
        var content = await response.Content.ReadAsByteArrayAsync();
        check(response.StatusCode == HttpStatusCode.OK && response.Content.Headers.ContentDisposition?.DispositionType == "attachment" &&
          response.Headers.CacheControl?.NoStore == true && response.Headers.GetValues("X-Guard-Export-Truncated").Single() == "true" && content.Length <= 8 * 1024 * 1024,
          "Root receives bounded JSONL attachment with no-store and explicit truncation signal");
        check(audit.Events.TakeLast(2).Select(e => e.EventType).SequenceEqual([Guard.Core.Enums.AuditEventType.ExportDeliveryStarted, Guard.Core.Enums.AuditEventType.ExportDeliveryCompleted]),
          "technical export audits start before writing and completion after server flush");
      }
      using (var local = await Send(token, source: "LocalBuffer")) {
        var localText = await local.Content.ReadAsStringAsync();
        check(local.StatusCode == HttpStatusCode.OK && localText.Contains("\"Source\":\"LocalBuffer\"") && local.Headers.CacheControl?.NoStore == true,
          "Root can explicitly request a protected local-buffer attachment through HTTP");
      }
      using (var invalidSource = await Send(token, source: "../../appsettings.json")) check(invalidSource.StatusCode == HttpStatusCode.Conflict && invalidSource.Content.Headers.ContentDisposition == null,
        "HTTP source cannot be interpreted as a filesystem path");
      audit.AfterRecord = entry => { if (entry.EventType == Guard.Core.Enums.AuditEventType.ExportDeliveryStarted) endpointAccess.Root = false; };
      using (var revoked = await Send(token, source: "LocalBuffer")) check(revoked.StatusCode == HttpStatusCode.Forbidden && revoked.Content.Headers.ContentDisposition == null,
        "Root revoked during delivery audit receives no diagnostic attachment bytes");
      audit.AfterRecord = null; endpointAccess.Root = true;
      audit.Fail = true;
      using (var failed = await Send(token)) check(failed.StatusCode != HttpStatusCode.OK && failed.Content.Headers.ContentDisposition == null,
        "mandatory audit failure prevents diagnostic attachment bytes");
      audit.Fail = false;
    } finally { audit.Fail = false; audit.AfterRecord = null; await app.StopAsync(); }
  }
  private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logs, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logs, encoder)
  {
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
      var actor = Request.Headers["X-Test-Actor"].ToString();
      if (actor.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
      var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor), new("root", actor == "root" ? "true" : "false")], Scheme.Name);
      return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), Scheme.Name)));
    }
  }
}
