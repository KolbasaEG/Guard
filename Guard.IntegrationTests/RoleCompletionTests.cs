using Guard.Core.Contexts;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class RoleCompletionTests
{
  private static readonly string[] Failures = ["none", "audit", "notification", "both", "cancelled"];

  public static async Task RunAsync(IServiceProvider provider, IDbContextFactory<ApplicationDbContext> factory,
      Action<bool, string> check)
  {
    var roles = provider.GetRequiredService<IRoleAccessService>();
    var original = (await roles.GetUserRolesAsync("unassigned")).Selected.ToArray();
    var created = new List<string>();
    try
    {
      foreach (var failure in Failures)
      {
        var delivery = new Delivery(failure);
        var service = Service(provider, delivery);
        var name = $"Completion {failure}";
        delivery.Observe = async () => {
          await using var db = await factory.CreateDbContextAsync();
          return await db.Roles.AnyAsync(r => r.Name == name);
        };
        var id = await service.SaveAsync(new() { Name = name, Permissions = [Permissions.Personals.Read] });
        created.Add(id);
        delivery.Assert(check, "create");
        check((await roles.GetAsync(id)).Permissions.SetEquals([Permissions.Personals.Read]),
            $"role create remains persisted after {failure} delivery");

        var edit = await roles.GetAsync(id);
        edit.Permissions = [Permissions.Subdivisions.Read];
        delivery.Reset(async () => {
          await using var db = await factory.CreateDbContextAsync();
          return await db.RoleClaims.AnyAsync(c => c.RoleId == id && c.ClaimValue == Permissions.Subdivisions.Read) &&
              !await db.RoleClaims.AnyAsync(c => c.RoleId == id && c.ClaimValue == Permissions.Personals.Read);
        });
        check(await service.SaveAsync(edit) == id, $"role edit returns committed ID after {failure} delivery");
        delivery.Assert(check, "edit");
        check((await roles.GetAsync(id)).Permissions.SetEquals([Permissions.Subdivisions.Read]),
            $"role permissions remain persisted after {failure} delivery");

        delivery.Reset(() => Task.FromResult(true));
        await PostgresTests.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(edit), check,
            $"stale role edit still fails before commit with {failure} delivery configured");
        check(delivery.NotificationCalls == 0 && delivery.AuditCalls == 0 && delivery.Errors.Count == 0,
            "failed role write does not publish committed events or suppress concurrency error");

        var assignment = await roles.GetUserRolesAsync("unassigned");
        delivery.Reset(async () => {
          await using var db = await factory.CreateDbContextAsync();
          return await db.UserRoles.AnyAsync(r => r.UserId == "unassigned" && r.RoleId == id);
        });
        await service.SetUserRolesAsync("unassigned", [id], assignment.Version);
        delivery.Assert(check, "assignment");
        check(delivery.Targets.SequenceEqual(["unassigned"]) && delivery.EventType == AuditEventType.UserRolesChanged &&
            delivery.Actor == "root", "assignment delivery retains affected user and trusted actor");
        check((await roles.GetUserRolesAsync("unassigned")).Selected.SequenceEqual([id]),
            $"assignment remains persisted after {failure} delivery");

        delivery.Reset(async () => {
          await using var db = await factory.CreateDbContextAsync();
          return !await db.Roles.AnyAsync(r => r.Id == id) && !await db.UserRoles.AnyAsync(r => r.RoleId == id);
        });
        await service.DeleteAsync(id);
        delivery.Assert(check, "delete");
        check(delivery.Targets.SequenceEqual(["unassigned"]) && delivery.EventType == AuditEventType.RolePermissionsChanged,
            "deletion notifies previously assigned users after committed cascade deletion");
        await using var verify = await factory.CreateDbContextAsync();
        check(!await verify.Roles.AnyAsync(r => r.Id == id), $"role deletion remains persisted after {failure} delivery");
        created.Remove(id);
      }
    }
    finally
    {
      var assignment = await roles.GetUserRolesAsync("unassigned");
      await roles.SetUserRolesAsync("unassigned", original, assignment.Version);
      foreach (var id in created) await roles.DeleteAsync(id);
    }
  }

  // Вызывается в точке существующего теста с пустыми permissions Администратора.
  public static async Task InitializeAsync(IServiceProvider provider, IDbContextFactory<ApplicationDbContext> factory,
      Action<bool, string> check)
  {
    foreach (var failure in Failures)
    {
      await using (var db = await factory.CreateDbContextAsync())
        await db.RoleClaims.Where(c => c.RoleId == "admin-role").ExecuteDeleteAsync();
      var delivery = new Delivery(failure);
      delivery.Observe = async () => {
        await using var db = await factory.CreateDbContextAsync();
        return await db.RoleClaims.CountAsync(c => c.RoleId == "admin-role" && c.ClaimType == "Permission") ==
            PermissionCatalog.All.Count(p => !p.RootOnly);
      };
      var service = Service(provider, delivery);
      await service.InitializeAdministratorAsync();
      delivery.Assert(check, "initialization");
      delivery.Reset(() => Task.FromResult(true));
      await PostgresTests.ThrowsAsync<InvalidOperationException>(() => service.InitializeAdministratorAsync(), check,
          "initialization retry is rejected after committed write despite delivery failure");
      check(delivery.NotificationCalls == 0 && delivery.AuditCalls == 0, "rejected initialization publishes no completion events");
    }
  }

  private static RoleAccessService Service(IServiceProvider provider, Delivery delivery) => new(
      provider.GetRequiredService<IPermissionService>(), provider.GetRequiredService<IServiceScopeFactory>(),
      delivery, delivery, delivery);

  private sealed class Delivery(string failure) : IAuditService, IAccessChangeNotifier, ILogger<RoleAccessService>
  {
    public Func<Task<bool>> Observe = () => Task.FromResult(false);
    public int NotificationCalls, AuditCalls;
    public bool NotificationSawCommit, AuditSawCommit;
    public string[] Targets = [];
    public string? Actor;
    public AuditEventType EventType;
    public List<Exception?> Errors { get; } = [];
    public void Reset(Func<Task<bool>> observe)
    {
      Observe = observe;
      NotificationCalls = AuditCalls = 0;
      NotificationSawCommit = AuditSawCommit = false;
      Targets = [];
      Errors.Clear();
    }
    public async Task PublishAsync(IEnumerable<string> userIds)
    {
      NotificationCalls++;
      Targets = userIds.ToArray();
      NotificationSawCommit = await Observe();
      if (failure == "cancelled") throw new OperationCanceledException("Synthetic notification cancellation after commit");
      if (failure is "notification" or "both") throw new IOException("Synthetic notification delivery failure");
    }
    public void LogIdentityEvent(AuditEventType eventType, string? userName, string? ipAddress = null, string? details = null)
    {
      AuditCalls++;
      Actor = userName;
      EventType = eventType;
      AuditSawCommit = Observe().GetAwaiter().GetResult();
      if (failure == "cancelled") throw new OperationCanceledException("Synthetic audit cancellation after commit");
      if (failure is "audit" or "both") throw new IOException("Synthetic audit delivery failure");
    }
    public void Assert(Action<bool, string> check, string operation)
    {
      check(NotificationCalls == 1 && AuditCalls == 1, $"{operation}: both deliveries attempted once with {failure}");
      check(NotificationSawCommit && AuditSawCommit, $"{operation}: separate contexts see commit before delivery with {failure}");
      var expected = failure switch { "none" => 0, "both" or "cancelled" => 2, _ => 1 };
      check(Errors.Count == expected && Errors.All(e => e != null), $"{operation}: each delivery failure logged with exception");
    }
    public IDisposable Subscribe(string userId, Func<Task> refresh) => throw new NotSupportedException();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
      if (logLevel >= LogLevel.Error) Errors.Add(exception);
    }
  }
}
