using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

internal static class IdentityRollbackIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, IPermissionService permissions,
    IDataAccessScopeService access, Action<bool, string> check)
  {
    var fault = new Fault();
    var delivery = new Delivery();
    var services = new ServiceCollection().AddLogging();
    services.AddScoped(_ => factory.CreateDbContext());
    services.AddDataProtection().UseEphemeralDataProtectionProvider();
    services.AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>()
      .AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
    services.AddSingleton(fault);
    services.AddScoped<IUserValidator<ApplicationUser>, FailingValidator>();
    await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    var service = new SecurityService(null!, null!, null!, null!, null!, null!, permissions, access, null!,
      provider.GetRequiredService<IServiceScopeFactory>(), delivery, delivery, null!, NullLogger<SecurityService>.Instance);
    foreach (var scenario in new[] { "edit", "reset", "block", "unblock" }) {
      var name = "identity-rollback-" + Guid.NewGuid().ToString("N");
      var role = new ApplicationRole { Name = name + "-role" };
      var user = new ApplicationUser { UserName = name, Email = name + "@example.test", EmailConfirmed = true,
        LockoutEnabled = true, AccessFailedCount = scenario == "unblock" ? 3 : 0,
        AccountBlockReason = scenario == "unblock" ? "Administrator" : null,
        LockoutEnd = scenario == "unblock" ? DateTimeOffset.MaxValue : null };
      await using (var seed = provider.CreateAsyncScope()) {
        var manager = seed.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await manager.CreateAsync(user, "InitialPassword123!");
        check(result.Succeeded, "rollback fixture uses real Identity password hash");
        check((await seed.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>().CreateAsync(role)).Succeeded &&
          (await manager.AddToRoleAsync(user, role.Name!)).Succeeded, "rollback fixture has an assigned role");
      }
      try {
        await using var observer = await factory.CreateDbContextAsync();
        async Task<ApplicationUser> Read() => await observer.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        var before = await Read();
        var original = JsonSerializer.Serialize(before);
        var roles = await observer.UserRoles.AsNoTracking().Where(r => r.UserId == user.Id).ToArrayAsync();
        fault.Calls = 0;
        // Fail security-stamp update for edit/block/unblock, final flag update for reset.
        fault.FailAt = scenario switch { "block" => 3, "unblock" => 4, _ => 2 };
        fault.BeforeFailure = async db => {
          var intermediate = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
          check(intermediate.ConcurrencyStamp != before.ConcurrencyStamp, scenario + ": prior Identity step reached SQL inside transaction");
          check(scenario switch {
            "edit" => intermediate.Email == name + "changed@example.test" && !intermediate.EmailConfirmed,
            "reset" => intermediate.PasswordHash != before.PasswordHash && intermediate.SecurityStamp != before.SecurityStamp,
            "block" => intermediate.AccountBlockReason == "Administrator" && intermediate.LockoutEnd > DateTimeOffset.UtcNow,
            _ => intermediate.AccountBlockReason == null && intermediate.LockoutEnd == null && intermediate.AccessFailedCount == 0 && intermediate.UnblockedAtUtc != null
          }, scenario + ": SQL contains the expected intermediate change");
          check(JsonSerializer.Serialize(await Read()) == original, scenario + ": independent connection cannot see uncommitted SQL");
        };
        Task<IdentityResult> Execute() => scenario switch {
          "edit" => service.UpdateUserDetailsAsync(new() { Id = user.Id, UserName = name,
            Email = name + "changed@example.test", Version = before.ConcurrencyStamp! }),
          "reset" => service.ResetPasswordAsync(user.Id, "ReplacementPassword123!"),
          _ => service.ToggleUserLockoutAsync(user.Id, scenario == "block")
        };
        delivery.Audits = delivery.Notifications = 0;
        await PostgresTests.ThrowsAsync<UserSecurityException>(async () => { await Execute(); }, check,
          scenario + ": failed mandatory IdentityResult rejects compound operation");
        check(fault.Calls == fault.FailAt, scenario + ": intended mandatory step was reached");
        check(JsonSerializer.Serialize(await Read()) == original, scenario + ": rollback restores all user fields, hash, concurrency and security stamps");
        check((await observer.UserRoles.AsNoTracking().Where(r => r.UserId == user.Id).ToArrayAsync())
          .Select(r => r.RoleId).Order().SequenceEqual(roles.Select(r => r.RoleId).Order()), scenario + ": rollback preserves role assignments");
        check(delivery.Audits == 0 && delivery.Notifications == 0, scenario + ": rollback emits no success audit or access notification");
        fault.FailAt = 0; fault.BeforeFailure = null;
        check((await Execute()).Succeeded, scenario + ": same service succeeds after fault removal and scope cleanup");
        check(delivery.Audits == 1 && delivery.Notifications == 1 && JsonSerializer.Serialize(await Read()) != original,
          scenario + ": successful retry commits and publishes once");
      }
      finally {
        fault.FailAt = 0; fault.BeforeFailure = null;
        await using var cleanup = await factory.CreateDbContextAsync();
        await cleanup.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();
        await cleanup.Roles.Where(r => r.Id == role.Id).ExecuteDeleteAsync();
      }
    }
  }
  private sealed class Fault
  {
    public int Calls, FailAt;
    public Func<ApplicationDbContext, Task>? BeforeFailure;
  }
  private sealed class FailingValidator(ApplicationDbContext db, Fault fault) : IUserValidator<ApplicationUser>
  {
    public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user) {
      if (++fault.Calls != fault.FailAt) return IdentityResult.Success;
      await fault.BeforeFailure!(db);
      return IdentityResult.Failed(new IdentityError { Code = "SyntheticMandatoryStepFailure", Description = "Тестовый отказ обязательного шага." });
    }
  }
  private sealed class Delivery : IAuditService, IAccessChangeNotifier
  {
    public int Audits, Notifications;
    public void LogIdentityEvent(AuditEventType type, string? user, string? ip = null, string? details = null) => Audits++;
    public Task PublishAsync(IEnumerable<string> ids) { Notifications++; return Task.CompletedTask; }
    public IDisposable Subscribe(string id, Func<Task> refresh) => throw new NotSupportedException();
  }
}
