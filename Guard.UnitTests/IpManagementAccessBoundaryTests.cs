using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;

internal static class IpManagementAccessBoundaryTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    using var cancellation = new CancellationTokenSource();
    var token = cancellation.Token;
    var permissions = new Permissions();
    var scopes = new Scopes();
    var service = new IpManagementAccessService(permissions, scopes);
    foreach (var write in new[] { false, true })
      foreach (var root in new[] { false, true })
        foreach (var empty in new[] { false, true })
        {
          scopes.Scope = new("trusted-actor", root, empty ? [] : [Guid.NewGuid(), Guid.NewGuid()]);
          var calls = scopes.Calls;
          var permissionCalls = permissions.Calls;
          var result = await service.GetScopeAsync(write, token);
          check(permissions.Calls == permissionCalls + 1 && permissions.Permission == (write ? Guard.Core.Identity.Permissions.IpAddresses.Manage : Guard.Core.Identity.Permissions.IpAddresses.Read),
            "IP management requires the exact read or manage permission, including Root");
          check(result.UserId == scopes.Scope.UserId && result.IsRoot == root && result.SubdivisionIds.SetEquals(scopes.Scope.SubdivisionIds),
            "IP management preserves trusted actor, Root and empty or restricted subdivision scope");
          check(scopes.Calls == calls + 1 && permissions.Token == token && scopes.Token == token,
            "IP management forwards cancellation to both stages and reads scope once");
        }

    foreach (var failure in new Exception[] { new UnauthorizedAccessException(), new InvalidOperationException("permission failure"), new OperationCanceledException(token) })
    {
      permissions.Require = _ => Task.FromException(failure);
      var calls = scopes.Calls;
      await ExpectSameAsync(() => service.GetScopeAsync(true, token), failure, check);
      check(scopes.Calls == calls, "permission failure prevents all scope reads");
    }
    permissions.Require = _ => Task.CompletedTask;
    foreach (var failure in new Exception[] { new UnauthorizedAccessException(), new InvalidOperationException("scope failure"), new OperationCanceledException(token) })
    {
      scopes.Read = _ => Task.FromException<DataAccessScope>(failure);
      await ExpectSameAsync(() => service.GetScopeAsync(false, token), failure, check);
    }
    scopes.Read = null;

    // A pending authorization must prevent the next stage, not just precede it in invocation order.
    var permissionGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    permissions.Require = _ => permissionGate.Task;
    var before = scopes.Calls;
    var pending = service.GetScopeAsync(true, token);
    check(!pending.IsCompleted && scopes.Calls == before, "scope is not read while permission check is pending");
    permissionGate.SetResult();
    await pending;
    check(scopes.Calls == before + 1, "scope is read only after permission success");

    permissions.Require = ct => { ct.ThrowIfCancellationRequested(); return Task.CompletedTask; };
    cancellation.Cancel();
    before = scopes.Calls;
    await TestAssertions.ThrowsAsync<OperationCanceledException>(() => service.GetScopeAsync(false, token), check,
      "already canceled request propagates through permission check");
    check(scopes.Calls == before, "canceled permission check prevents scope read");

    using var between = new CancellationTokenSource();
    permissions.Require = _ => { between.Cancel(); return Task.CompletedTask; };
    scopes.Read = ct => { ct.ThrowIfCancellationRequested(); return Task.FromResult(scopes.Scope); };
    try { await service.GetScopeAsync(true, between.Token); check(false, "cancellation between stages must propagate"); }
    catch (OperationCanceledException error) { check(error.CancellationToken == between.Token, "scope receives cancellation occurring after authorization"); }
  }

  private static async Task ExpectSameAsync(Func<Task> action, Exception expected, Action<bool, string> check)
  {
    try { await action(); }
    catch (Exception actual) { check(ReferenceEquals(actual, expected), "dependency error propagates without an allowed or empty success"); return; }
    check(false, "dependency error must propagate");
  }

  private sealed class Permissions : IPermissionService
  {
    public string? Permission;
    public int Calls;
    public CancellationToken Token;
    public Func<CancellationToken, Task> Require = _ => Task.CompletedTask;
    public Task RequireAsync(string permission, CancellationToken ct = default)
    { Calls++; Permission = permission; Token = ct; return Require(ct); }
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<UserAccessSnapshot> GetForUserAsync(string userId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> HasAsync(string permission, CancellationToken ct = default) => throw new NotSupportedException();
  }

  private sealed class Scopes : IDataAccessScopeService
  {
    public DataAccessScope Scope = new("trusted-actor", false, []);
    public int Calls;
    public CancellationToken Token;
    public Func<CancellationToken, Task<DataAccessScope>>? Read;
    public Task<DataAccessScope> GetAsync(CancellationToken ct = default)
    { Calls++; Token = ct; return Read?.Invoke(ct) ?? Task.FromResult(Scope); }
  }
}
