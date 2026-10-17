using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Guard.Core.Identity;
using Microsoft.Extensions.Logging.Abstractions;

internal static class ApiDiagnosticTests
{
  public static async Task CheckAccessAsync(Action<bool, string> check)
  {
    var permissions = new PermissionsStub();
    var service = new ApiDiagnosticService(null!, permissions, null!, null!, NullLogger<ApiDiagnosticService>.Instance);
    try { await service.RunAsync(new(1978, 10, 9)); check(false, "non-Root diagnostics rejected on server"); }
    catch (UnauthorizedAccessException) { check(true, "non-Root diagnostics rejected before database or HTTP"); }
    permissions.Root = true;
    try { await service.RunAsync(default); check(false, "diagnostic service validates mandatory date"); }
    catch (ArgumentException) { check(true, "diagnostic service validates mandatory date before database or HTTP"); }
    try { await service.RunAsync(new(1978, 10, 9)); check(false, "diagnostics require full log level"); }
    catch (EntityRuleException) { check(true, "diagnostics reject log level that suppresses the protocol"); }
  }

  private sealed class PermissionsStub : IPermissionService
  {
    public bool Root { get; set; }
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(new UserAccessSnapshot("diagnostic-test", Root, []));
    public Task<UserAccessSnapshot> GetForUserAsync(string userId, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string permission, CancellationToken ct = default) => Task.FromResult(Root);
    public Task RequireAsync(string permission, CancellationToken ct = default) => Root ? Task.CompletedTask : throw new UnauthorizedAccessException();
  }

}
