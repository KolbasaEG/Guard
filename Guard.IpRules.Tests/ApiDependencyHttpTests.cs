using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class ApiDependencyHttpTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    using var handler = new Handler();
    using var http = new HttpClient(handler);
    var client = new DirectoryApiClient(http, Options.Create(new DirectoryApiOptions()), NullLogger<DirectoryApiClient>.Instance);
    var snapshot = new ApiSnapshot([], [], [new() { Id = 10, SubdivisionId = 3, FirstName = "Test", LastName = "Employee",
      PositionType = 6, PositionCode = 10, UpdatedAt = DateTimeOffset.UtcNow }]) { UpdatedSince = new(2026, 10, 1), HeadSubdivisionId = 1 };
    var resolved = await client.ResolveDependenciesAsync(snapshot, new HashSet<(int, int)>(), new HashSet<long>(),
      "https://example.invalid/api", "test", "secret", CancellationToken.None);
    ApiSnapshotValidator.Validate(resolved);
    check(resolved.Subdivisions.Count == 3 && resolved.Classifiers.Count == 2 && handler.Calls == 5,
      "delta loads missing subdivision chain and both referenced classifiers once");
    check(handler.SafeRequestShape, "dependency requests use precise IDs, Basic auth and omit date filter");
    var before = handler.Calls;
    await client.ResolveDependenciesAsync(snapshot, new HashSet<(int, int)> { (6, 10), (362, 0) }, new HashSet<long> { 1, 2, 3 },
      "https://example.invalid/api", "test", "secret", CancellationToken.None);
    check(handler.Calls == before, "stored dependencies do not cause redundant HTTP requests");
    await client.PingAsync("https://example.invalid/api", "test", "secret", CancellationToken.None);
    check(handler.Pings == 1, "connection probe calls GET ping with Basic authentication");
    handler.NotFound = true;
    try { await client.ResolveDependenciesAsync(snapshot, new HashSet<(int, int)>(), new HashSet<long>(), "https://example.invalid/api", "test", "secret", CancellationToken.None); check(false, "missing dependency rejected"); }
    catch (EntityRuleException ex) { check(ex.Message.Contains("3"), "dependency 404 reports missing external ID safely"); }
    handler.NotFound = false;
    using var stopped = new CancellationTokenSource(); stopped.Cancel();
    before = handler.Calls;
    try { await client.ResolveDependenciesAsync(snapshot, new HashSet<(int, int)>(), new HashSet<long>(), "https://example.invalid/api", "test", "secret", stopped.Token); check(false, "cancelled dependency resolution stopped"); }
    catch (OperationCanceledException) { check(handler.Calls == before, "cancellation stops dependency requests before HTTP"); }
  }
  private sealed class Handler : HttpMessageHandler
  {
    public int Calls { get; private set; }
    public int Pings { get; private set; }
    public bool SafeRequestShape { get; private set; } = true;
    public bool NotFound { get; set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
      SafeRequestShape &= Equals(request.Headers.Authorization, new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("test:secret"))));
      if (request.Method == HttpMethod.Get) { Pings++; return new(HttpStatusCode.OK); }
      Calls++;
      if (NotFound) return new(HttpStatusCode.NotFound);
      using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
      var filter = json.RootElement.GetProperty("Data");
      SafeRequestShape &= !filter.TryGetProperty("UpdatedAt", out _) && filter.GetProperty("page").GetInt32() == 1;
      object item;
      if (request.RequestUri!.Segments.Last() == "subdivisions")
      {
        var id = filter.GetProperty("Id").GetInt64();
        item = new ApiSubdivision { Id = id, ParentId = id == 1 ? 999 : id - 1, Name = "Subdivision", StatusType = 362, StatusCode = 0, UpdatedAt = DateTimeOffset.UtcNow };
      }
      else
      {
        item = new RemoteClassifierDto { Type = filter.GetProperty("ClassifierType").GetInt32(), Code = filter.GetProperty("ClassifierCode").GetInt32(),
          ClassifierName = "Classifier", Value = "", IsActive = 1, UpdatedAt = DateTimeOffset.UtcNow };
      }
      return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { Success = true, Data = new[] { item },
        Pagination = new { Page = 1, PerPage = 500, TotalPages = 1, TotalCount = 1 } }) };
    }
  }
}
