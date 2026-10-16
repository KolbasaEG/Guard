using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.Extensions.DependencyInjection;

internal static class RoleInputSnapshotTests
{
  public static async Task RunAsync(IServiceProvider provider, Action<bool, string> check)
  {
    var roles = provider.GetRequiredService<IRoleAccessService>();
    var permissions = provider.GetRequiredService<IPermissionService>();
    var originalAssignments = (await roles.GetUserRolesAsync("unassigned")).Selected.ToArray();
    var ids = new List<string>();
    RoleAccessService Service(PausedPermissions paused) => new(paused,
        provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<IAuditService>(),
        provider.GetRequiredService<IAccessChangeNotifier>());
    try
    {
      var input = new RoleEditDto { Name = "Snapshot create", Permissions = [Permissions.Personals.Read] };
      var originalPermissions = input.Permissions;
      var paused = new PausedPermissions(permissions);
      var saving = Service(paused).SaveAsync(input);
      await paused.Entered.Task;
      input.Id = "root-role";
      input.Name = "Root";
      input.Version = "forged";
      originalPermissions.Clear();
      originalPermissions.Add("invented");
      input.Permissions = [Permissions.Roles.Manage];
      paused.Release.SetResult();
      var id = await saving;
      ids.Add(id);
      var stored = await roles.GetAsync(id);
      check(stored.Name == "Snapshot create" && stored.Permissions.SetEquals([Permissions.Personals.Read]),
          "role creation uses name and independent permission collection captured before authorization await");
      check(paused.Calls == 2, "role creation rechecks Root after transaction lock");

      // Проверяем изменения входа как при первой проверке, так и после блокировки.
      foreach (var pauseAt in new[] { 1, 2 })
      {
        input = await roles.GetAsync(id);
        input.Name = $"Snapshot edit {pauseAt}";
        input.Permissions = [Permissions.Subdivisions.Read];
        paused = new PausedPermissions(permissions, pauseAt);
        saving = Service(paused).SaveAsync(input);
        await paused.Entered.Task;
        input.Id = "root-role";
        input.Name = "Root";
        input.Version = "forged";
        input.Permissions.Clear();
        input.Permissions.Add("invented");
        paused.Release.SetResult();
        check(await saving == id, $"role edit retains target captured before await {pauseAt}");
        stored = await roles.GetAsync(id);
        check(stored.Name == $"Snapshot edit {pauseAt}" && stored.Permissions.SetEquals([Permissions.Subdivisions.Read]),
            $"role edit retains captured version, name and permissions across await {pauseAt}");
      }

      input = await roles.GetAsync(id);
      input.Version = "stale";
      paused = new PausedPermissions(permissions);
      saving = Service(paused).SaveAsync(input);
      await paused.Entered.Task;
      input.Version = stored.Version;
      paused.Release.SetResult();
      await PostgresTests.ThrowsAsync<InvalidOperationException>(() => saving, check,
          "replacing stale input version while waiting cannot bypass role concurrency check");

      input = new() { Name = "Invalid snapshot", Permissions = ["invented"] };
      paused = new PausedPermissions(permissions);
      saving = Service(paused).SaveAsync(input);
      await paused.Entered.Task;
      input.Permissions.Clear();
      input.Permissions.Add(Permissions.Personals.Read);
      paused.Release.SetResult();
      await PostgresTests.ThrowsAsync<ArgumentException>(() => saving, check,
          "permission validation uses original request even when form changes to valid values");

      var assignments = await roles.GetUserRolesAsync("unassigned");
      var selected = new List<string> { id, id };
      paused = new PausedPermissions(permissions);
      var assigning = Service(paused).SetUserRolesAsync("unassigned", selected, assignments.Version);
      await paused.Entered.Task;
      selected.Clear();
      selected.Add("root-role");
      paused.Release.SetResult();
      await assigning;
      check((await roles.GetUserRolesAsync("unassigned")).Selected.SequenceEqual([id]),
          "user role assignment snapshots and deduplicates mutable selection before authorization await");
      check(paused.Calls == 2, "role assignment rechecks Root after transaction lock");
    }
    finally
    {
      var assignments = await roles.GetUserRolesAsync("unassigned");
      await roles.SetUserRolesAsync("unassigned", originalAssignments, assignments.Version);
      foreach (var id in ids) await roles.DeleteAsync(id);
    }
  }

  private sealed class PausedPermissions(IPermissionService inner, int pauseAt = 1) : IPermissionService
  {
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Calls { get; private set; }
    public async Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default)
    {
      if (++Calls == pauseAt)
      {
        Entered.SetResult();
        await Release.Task.WaitAsync(ct);
      }
      return await inner.GetCurrentAsync(ct);
    }
    public Task<UserAccessSnapshot> GetForUserAsync(string userId, CancellationToken ct = default) => inner.GetForUserAsync(userId, ct);
    public Task<bool> HasAsync(string permission, CancellationToken ct = default) => inner.HasAsync(permission, ct);
    public Task RequireAsync(string permission, CancellationToken ct = default) => inner.RequireAsync(permission, ct);
  }
}
