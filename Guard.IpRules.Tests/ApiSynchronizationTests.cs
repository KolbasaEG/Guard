using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

internal static class ApiSynchronizationTests
{
  internal static ApiSnapshot Snapshot() => new(
    [new() { Type = 6, Code = 0, ClassifierName = "ДОЛЖНОСТЬ", Value = "", IsActive = 1, UpdatedAt = Stamp },
     new() { Type = 6, Code = 10, ClassifierName = "ДОЛЖНОСТЬ", Value = "Инспектор", IsActive = 1, UpdatedAt = Stamp },
     new() { Type = 363, Code = 1, ClassifierName = "ТИП ОРГАНА", Value = "Управление", IsActive = 1, UpdatedAt = Stamp }],
    [new() { Id = 100, Name = "Организация", OrganTypeId = 363, OrganTypeCode = 1, UpdatedAt = Stamp },
     new() { Id = 200, ParentId = 100, Name = "Первый отдел", UpdatedAt = Stamp },
     new() { Id = 300, ParentId = 100, Name = "Второй отдел", UpdatedAt = Stamp }],
    [new() { Id = 10, SubdivisionId = 200, LastName = "Первый", FirstName = "Сотрудник", PositionType = 6, PositionCode = 10, UpdatedAt = Stamp },
     new() { Id = 20, SubdivisionId = 200, LastName = "Второй", FirstName = "Сотрудник", UpdatedAt = Stamp }]);

  private static readonly DateTimeOffset Stamp = new(2026, 10, 8, 15, 0, 0, TimeSpan.FromHours(3));

  public static async Task RunAsync(Action<bool, string> check)
  {
    var snapshot = Snapshot();
    ApiSnapshotValidator.Validate(snapshot);
    check(ApiSnapshotApplier.Descendants(snapshot.Subdivisions.ToDictionary(s => s.Id), 100).SetEquals([100, 200, 300]), "API subtree includes root and descendants");
    check(ApiSnapshotApplier.Descendants(snapshot.Subdivisions.ToDictionary(s => s.Id), 200).SetEquals([200]), "API pool excludes siblings");
    void Reject(ApiSnapshot invalid, string name)
    {
      try { ApiSnapshotValidator.Validate(invalid); check(false, name); }
      catch (Exception ex) when (ex is EntityRuleException or ArgumentException) { check(true, name); }
    }
    Reject(snapshot with { Classifiers = snapshot.Classifiers.Concat([snapshot.Classifiers[0]]).ToList() }, "duplicate classifier keys rejected");
    Reject(snapshot with { Subdivisions = [new() { Id = 1, ParentId = 2, Name = "Missing", UpdatedAt = Stamp }] }, "unknown parent rejected");
    Reject(snapshot with { Subdivisions = [new() { Id = 1, ParentId = 2, Name = "A", UpdatedAt = Stamp }, new() { Id = 2, ParentId = 1, Name = "B", UpdatedAt = Stamp }] }, "cyclic hierarchy rejected");
    Reject(snapshot with { Personnel = [new() { Id = 1, SubdivisionId = 200, FirstName = "A", LastName = "B", PositionType = 6, PositionCode = 999, UpdatedAt = Stamp }] }, "unknown classifier reference rejected");
    Reject(snapshot with { Personnel = [new() { Id = 1, SubdivisionId = 999, FirstName = "A", LastName = "B", UpdatedAt = Stamp }] }, "unknown employee subdivision rejected");
    Reject(snapshot with { Personnel = [new() { Id = 1, SubdivisionId = 200, FirstName = "A", LastName = new string('x', 101), UpdatedAt = Stamp }] }, "API input respects database length limits");
    Reject(snapshot with { Classifiers = [new() { Type = 6, Code = 1, IsActive = 2, UpdatedAt = Stamp }] }, "unknown active state rejected");
    var settings = new SynchronizationSettings { Mode = DataManagementMode.Api, ScheduleEnabled = true, DailyTime = new(2, 0) };
    var localDay = new DateTime(2026, 10, 9);
    DateTimeOffset Utc(DateTime local) => new(TimeZoneInfo.ConvertTimeToUtc(local, TimeZoneInfo.Local));
    check(!SynchronizationSchedule.IsDue(settings, Utc(localDay.AddHours(1))), "schedule waits for server local time");
    check(SynchronizationSchedule.IsDue(settings, Utc(localDay.AddHours(2))), "schedule runs at server 02:00");
    settings.LastScheduledDate = DateOnly.FromDateTime(localDay);
    check(!SynchronizationSchedule.IsDue(settings, Utc(localDay.AddHours(3))), "successful scheduled date prevents duplicate execution");
    settings.Mode = DataManagementMode.Local;
    check(!SynchronizationSchedule.IsDue(settings, Utc(localDay.AddDays(1).AddHours(3))), "local mode disables schedule");

    using var handler = new Handler(snapshot);
    using var http = new HttpClient(handler);
    var options = Options.Create(new DirectoryApiOptions { BaseUrl = "https://example.invalid/api", Username = "test", Password = "test", PageSize = 2, CompleteSnapshotGuaranteed = true });
    var diagnostics = new DiagnosticLogger();
    var client = new DirectoryApiClient(http, options, diagnostics);
    options.Value.BaseUrl = "https://unused.invalid/unused";
    options.Value.PageSize = 500;
    options.Value.Username = "unused"; options.Value.Password = "unused";
    var loaded = await client.FetchAsync("https://example.invalid/api", 2, CancellationToken.None, "test", "test");
    check(handler.ValidRequests, "stored Basic credentials override environment configuration on every request");
    options.Value.Username = "test"; options.Value.Password = "test";
    check(handler.RequestDates.All(d => d == null), "manual request without date omits UpdatedAt entirely");
    await client.FetchAsync("https://example.invalid/api", 2, CancellationToken.None, "test", "test", new DateOnly(2000, 8, 10));
    check(handler.RequestDates.TakeLast(5).All(d => d == "2000-08-10"), "UpdatedAt date is sent on every page of all three resources");
    handler.CallsOffset = 6;
    check(handler.ValidRequests && handler.Calls - handler.CallsOffset == 5, "stored source and page size override HTTP configuration for all resources");
    options.Value.BaseUrl = "https://example.invalid/api";
    options.Value.PageSize = 2;
    check(loaded.Classifiers.Count == 3 && loaded.Subdivisions.Count == 3 && loaded.Personnel.Count == 2 && handler.Calls - handler.CallsOffset == 5, "HTTP client collects every page of all three resources");
    check(diagnostics.Messages.Any(m => m.Contains("personnel") && m.Contains("HTTP 200")) &&
      diagnostics.Messages.Any(m => m.Contains("страница") && m.Contains("попытка")), "API diagnostics identify resource, page, attempt and HTTP response");
    check(handler.ValidRequests, "HTTP requests use correct endpoint, page envelope and Basic authentication");
    check(loaded.Classifiers[0].UpdatedAt.UtcDateTime.Hour == 12, "API timezone offset is converted to UTC");
    var target = new Personal { MiddleName = "Old", PositionType = 6, PositionCode = 10 };
    loaded.Personnel[1].Fields(null).ApplyTo(target);
    check(target.MiddleName == null && target.PositionType == null && target.PositionCode == null, "full API record clears absent optional fields");
    handler.FailSecondPage = true;
    try { await client.FetchAsync(CancellationToken.None); check(false, "failed later page cannot produce partial snapshot"); }
    catch (EntityRuleException) { check(true, "failed later page cannot produce partial snapshot"); }
    handler.FailSecondPage = false; handler.BadCount = true;
    try { await client.FetchAsync(CancellationToken.None); check(false, "inconsistent pagination rejected"); }
    catch (EntityRuleException) { check(true, "inconsistent pagination rejected"); }
    options.Value.CompleteSnapshotGuaranteed = false;
    try { await client.FetchAsync(CancellationToken.None); check(false, "unconfirmed snapshot contract blocks archival synchronization"); }
    catch (EntityRuleException) { check(true, "unconfirmed snapshot contract blocks archival synchronization"); }
    options.Value.CompleteSnapshotGuaranteed = true;
    handler.CancelRequest = true;
    try { await client.FetchAsync("https://example.invalid/api", 2, CancellationToken.None, "diagnostic-user", "diagnostic-secret"); check(false, "HTTP cancellation propagated"); }
    catch (OperationCanceledException) { check(true, "HTTP cancellation propagated"); }
    check(diagnostics.Messages.Any(m => m.Contains("HttpTimeoutOrTransportCancellation")), "diagnostics distinguish transport cancellation from operation token cancellation");
    using (var cancelled = new CancellationTokenSource())
    {
      cancelled.Cancel();
      try { await client.FetchAsync("https://example.invalid/api", 2, cancelled.Token, "diagnostic-user", "diagnostic-secret"); }
      catch (OperationCanceledException) { }
    }
    check(diagnostics.Messages.Any(m => m.Contains("OperationToken")), "diagnostics identify cancelled operation token");
    check(!diagnostics.Messages.Any(m => m.Contains("diagnostic-user") || m.Contains("diagnostic-secret") || m.Contains("Authorization")), "API diagnostic messages do not contain credentials or authorization header");

    handler.CancelRequest = false; handler.BadCount = false;
    options.Value.CompleteSnapshotGuaranteed = false;
    snapshot.Subdivisions[0].ParentId = 999;
    var beforeDiagnostic = handler.Calls;
    var diagnosticSnapshot = await client.FetchDiagnosticAsync("https://example.invalid/api", 2, "test", "test", new DateOnly(1978, 10, 9), CancellationToken.None);
    check(handler.Calls - beforeDiagnostic == 6 && diagnosticSnapshot.Personnel.Count == 2,
      "diagnostics finish every resource before validating broken references and do not require archival guarantee");
    check(handler.RequestDates.TakeLast(6).All(d => d == "1978-10-09"), "diagnostic date is mandatory on every resource page including exits");
    beforeDiagnostic = handler.Calls;
    try { await client.FetchDiagnosticAsync("https://example.invalid/api", 2, "test", "test", default, CancellationToken.None); check(false, "diagnostic default date rejected"); }
    catch (ArgumentException) { check(handler.Calls == beforeDiagnostic, "diagnostic default date rejected before HTTP"); }
    handler.FailSecondPage = true;
    try { await client.FetchDiagnosticAsync("https://example.invalid/api", 2, "test", "test", new DateOnly(1978, 10, 9), CancellationToken.None); check(false, "diagnostic incomplete pages rejected"); }
    catch (EntityRuleException) { check(true, "diagnostic incomplete pages cannot become a complete snapshot"); }
    handler.FailSecondPage = false;
    using (var stopDiagnostic = new CancellationTokenSource())
    {
      stopDiagnostic.Cancel();
      try { await client.FetchDiagnosticAsync("https://example.invalid/api", 2, "test", "test", new DateOnly(1978, 10, 9), stopDiagnostic.Token); check(false, "diagnostic cancellation propagated"); }
      catch (OperationCanceledException) { check(true, "diagnostic cancellation propagated"); }
    }

    using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options);
    var model = db.Model;
    var person = model.FindEntityType(typeof(Personal))!;
    check(!person.GetIndexes().Single(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(Personal.PersonalSubdivisionId)).IsUnique,
      "multiple employees may share external subdivision ID");
    check(person.GetIndexes().Single(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(Personal.ExternalId)).IsUnique,
      "external employee identities are unique");
    var subdivision = model.FindEntityType(typeof(Subdivision))!;
    check(subdivision.GetForeignKeys().Any(f => f.Properties.Select(p => p.Name).SequenceEqual(["OrganTypeId", "OrganTypeCode"]) && f.PrincipalEntityType.ClrType == typeof(Classifier)),
      "organ type links directly to classifier pair");
    check(!model.GetEntityTypes().Any(t => t.Name.EndsWith(".OrganType")), "obsolete organ type entity removed");
  }

  private sealed class Handler(ApiSnapshot snapshot) : HttpMessageHandler
  {
    public int Calls { get; private set; }
    public int CallsOffset { get; set; }
    public List<string?> RequestDates { get; } = [];
    public bool ValidRequests { get; private set; } = true;
    public bool FailSecondPage { get; set; }
    public bool BadCount { get; set; }
    public bool CancelRequest { get; set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
      Calls++;
      if (CancelRequest) throw new OperationCanceledException(ct);
      using var data = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
      var filter = data.RootElement.GetProperty("Data");
      RequestDates.Add(filter.TryGetProperty("UpdatedAt", out var date) ? date.GetString() : null);
      var page = filter.GetProperty("page").GetInt32(); var size = filter.GetProperty("per_page").GetInt32();
      ValidRequests &= request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.StartsWith("/api/") &&
        Equals(request.Headers.Authorization, new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test:test")))) && size == 2;
      object[] all = request.RequestUri!.Segments.Last() switch {
        "classifiers" => snapshot.Classifiers.Cast<object>().ToArray(), "subdivisions" => snapshot.Subdivisions.Cast<object>().ToArray(),
        "personnel" => snapshot.Personnel.Cast<object>().ToArray(), "out-of-scope" => snapshot.ExitEvents.Cast<object>().ToArray(), _ => throw new Exception("Unexpected resource")
      };
      var response = new { Success = !(FailSecondPage && page == 2), Data = all.Skip((page - 1) * size).Take(size),
        Pagination = new { Page = page, PerPage = size, TotalCount = all.Length + (BadCount ? 1 : 0), TotalPages = (int)Math.Ceiling((double)all.Length / size) } };
      return new(HttpStatusCode.OK) { Content = JsonContent.Create(response) };
    }
  }

  private sealed class DiagnosticLogger : Microsoft.Extensions.Logging.ILogger<DirectoryApiClient>
  {
    public List<string> Messages { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
      TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
  }
}
