using Guard.Core.Services;
using Guard.Core.Services.DTOs;

internal static class DataAccessScopeBoundaryTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    using var cancellation = new CancellationTokenSource();
    var token = cancellation.Token;
    var permissions = new Permissions();
    var reader = new Reader();
    var service = new DataAccessScopeService(permissions, reader);
    var own = Guid.NewGuid();
    var child = Guid.NewGuid();
    permissions.User = new("trusted-root", true, []);
    var scope = await service.GetAsync(token);
    check(scope.UserId == "trusted-root" && scope.IsRoot && scope.Allows(null) && scope.Allows(own), "Root has explicit full scope");
    check(reader.PathReads == 0 && reader.IdReads == 0, "Root does not query employee or tree");
    permissions.User = new("trusted-user", false, []);
    foreach (var path in new string?[] { null, "", "/", "///", "1/", "/1", "/0/", "/-1/", "/one/", "/9223372036854775808/" }) {
      reader.Path = path;
      scope = await service.GetAsync(token);
      check(!scope.IsRoot && scope.UserId == "trusted-user" && scope.SubdivisionIds.Count == 0 && !scope.Allows(null) && !scope.Allows(own),
        "missing or malformed path fails closed");
    }
    check(reader.IdReads == 0, "invalid path never queries descendants");
    reader.Path = "/1/12/";
    reader.Ids = [own, child, own];
    scope = await service.GetAsync(token);
    check(scope.SubdivisionIds.SetEquals([own, child]) && scope.Allows(own) && scope.Allows(child) && !scope.Allows(Guid.NewGuid()),
      "ordinary scope contains only returned distinct subdivision IDs");
    check(reader.UserId == "trusted-user" && reader.Prefix == reader.Path && reader.Token == token && permissions.Token == token,
      "scope forwards trusted actor, complete delimited path and token");
    reader.Ids = [];
    check((await service.GetAsync(token)).SubdivisionIds.Count == 0 && scope.SubdivisionIds.Count == 2,
      "new empty scope cannot mutate earlier snapshot");
    var before = reader.PathReads;
    permissions.Failure = new UnauthorizedAccessException();
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(token), check, "permission denial propagates");
    check(reader.PathReads == before, "denied actor does not read employee path");
    permissions.Failure = null;
    foreach (var failIds in new[] { false, true }) {
      reader.FailIds = failIds; reader.Failure = new InvalidOperationException("synthetic read failure");
      try { await service.GetAsync(token); check(false, "read failure propagates"); }
      catch (InvalidOperationException error) { check(ReferenceEquals(error, reader.Failure), "read failure never becomes an allowed or empty success"); }
      reader.Failure = new OperationCanceledException(token);
      try { await service.GetAsync(token); check(false, "read cancellation propagates"); }
      catch (OperationCanceledException error) { check(error.CancellationToken == token, "both read stages retain cancellation"); }
    }
    reader.Failure = null;
    cancellation.Cancel();
    await PostgresTests.ThrowsAsync<OperationCanceledException>(() => service.GetAsync(token), check, "permission cancellation propagates");
  }

  internal sealed class Permissions : IPermissionService
  {
    public UserAccessSnapshot User = new("trusted-user", false, []);
    public Exception? Failure; public CancellationToken Token;
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default)
    { Token = ct; ct.ThrowIfCancellationRequested(); if (Failure != null) throw Failure; return Task.FromResult(User); }
    public Task<UserAccessSnapshot> GetForUserAsync(string userId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> HasAsync(string permission, CancellationToken ct = default) => throw new NotSupportedException();
    public Task RequireAsync(string permission, CancellationToken ct = default) => throw new NotSupportedException();
  }
  private sealed class Reader : IDataAccessScopeReader
  {
    public string? Path; public IReadOnlyList<Guid> Ids = []; public bool FailIds; public Exception? Failure;
    public int PathReads; public int IdReads; public string? UserId; public string? Prefix; public CancellationToken Token;
    public Task<string?> FindUserPathAsync(string userId, CancellationToken ct = default)
    { PathReads++; UserId = userId; Token = ct; if (!FailIds && Failure != null) throw Failure; return Task.FromResult(Path); }
    public Task<IReadOnlyList<Guid>> GetSubdivisionIdsAsync(string path, CancellationToken ct = default)
    { IdReads++; Prefix = path; Token = ct; if (FailIds && Failure != null) throw Failure; return Task.FromResult(Ids); }
  }
}
