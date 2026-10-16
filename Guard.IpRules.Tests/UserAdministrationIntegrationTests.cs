using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

internal static class UserAdministrationIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    var validator = new CallbackValidator();
    var services = new ServiceCollection().AddLogging();
    services.AddScoped(_ => factory.CreateDbContext());
    services.AddDataProtection().UseEphemeralDataProtectionProvider();
    services.AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
    services.AddSingleton<IUserValidator<ApplicationUser>>(validator);
    await using var provider = services.BuildServiceProvider();
    var permissions = new MutablePermissions(); var access = new MutableScope(); var audit = new Audit();
    var service = new SecurityService(null!, null!, null!, null!, null!, null!, permissions, access, null!,
      provider.GetRequiredService<IServiceScopeFactory>(), audit, new AccessChangeNotifier(NullLogger<AccessChangeNotifier>.Instance), null!);
    IUserAdministrationService forms = service;
    var person = new Personal { FirstName = "Command", LastName = "User", CreatedBy = "root" };
    await using var db = await factory.CreateDbContextAsync();
    db.Personals.Add(person); await db.SaveChangesAsync();
    var prefix = "command-user-" + Guid.NewGuid().ToString("N");
    CreateUserCommand Create(string name, string password = "InitialPassword123!") => new(name, name + "@example.test", password, person.Id);
    try {
      foreach (var command in new[] { new CreateUserCommand("", "a@example.test", "Test123!", person.Id),
        new("x", "invalid", "Test123!", person.Id), new("x", "a@example.test", "Test123!", Guid.Empty),
        new("x", "a@example.test", "", person.Id), new(new string('x', 257), "a@example.test", "Test123!", person.Id) })
        await PostgresTests.ThrowsAsync<ArgumentException>(() => forms.CreateFromDtoAsync(command), check, "create validates fields on server");
      await PostgresTests.ThrowsAsync<UserSecurityException>(() => forms.CreateFromDtoAsync(Create(prefix, "x")), check, "DTO create applies Identity password policy");
      var legacy = new ApplicationUser { UserName = prefix, Email = prefix + "@example.test", PersonalId = person.Id };
      var originalId = legacy.Id;
      await PostgresTests.ThrowsAsync<InvalidOperationException>(() => service.CreateUserAsync(legacy, "InitialPassword123!", ["missing-role"]), check,
        "failed role assignment rolls back compound Identity creation");
      check(!await db.Users.AnyAsync(u => u.UserName == prefix) && legacy.Id == originalId && audit.Events.Count == 0,
        "failed compound create does not return new ID or success audit");
      validator.OnValidate = () => access.Value = access.Value with { SubdivisionIds = [Guid.NewGuid()] };
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => forms.CreateFromDtoAsync(Create(prefix)), check, "scope change before commit rolls back created user");
      check(!await db.Users.AnyAsync(u => u.UserName == prefix) && audit.Events.Count == 0, "rolled back creation leaves no user or success audit");
      validator.OnValidate = null; access.Value = new("root", true, []);
      using (var cancel = new CancellationTokenSource()) {
        validator.OnValidate = cancel.Cancel;
        await PostgresTests.ThrowsAsync<OperationCanceledException>(() => forms.CreateFromDtoAsync(Create(prefix), cancel.Token), check, "cancellation after Identity validation rolls back creation");
      }
      validator.OnValidate = null;
      check(!await db.Users.AnyAsync(u => u.UserName == prefix), "cancelled creation leaves no account");
      var id = await forms.CreateFromDtoAsync(Create(prefix));
      var saved = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
      check(saved.PersonalId == person.Id && !saved.EmailConfirmed && !await db.UserRoles.AnyAsync(r => r.UserId == id), "DTO creation binds employee without assigning roles or confirming email");
      check(audit.Events.Single() == (AuditEventType.UserCreated, "root", "Target=" + id), "creation audit uses trusted actor and committed ID");
      await PostgresTests.ThrowsAsync<InvalidOperationException>(() => forms.CreateFromDtoAsync(Create(prefix + "duplicate")), check, "DTO creation rejects occupied employee");
      var editor = (await forms.GetUserEditAsync(id)).User;
      var oldName = editor.UserName;
      validator.OnValidate = () => permissions.Value = new("other", true, []);
      editor.UserName = prefix + "changed";
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => forms.SaveDetailsAsync(editor), check, "actor change during edit rolls back fields and stamps");
      validator.OnValidate = null; permissions.Value = new("root", true, []);
      var unchanged = await db.Users.AsNoTracking().SingleAsync(u => u.Id == id);
      check(unchanged.UserName == oldName && unchanged.ConcurrencyStamp == editor.Version && unchanged.SecurityStamp == saved.SecurityStamp,
        "rejected edit preserves version and sessions");
      await forms.SaveDetailsAsync(editor);
      await PostgresTests.ThrowsAsync<InvalidOperationException>(() => forms.SaveDetailsAsync(editor), check, "DTO save rejects stale version");
      await PostgresTests.ThrowsAsync<UserSecurityException>(() => forms.ResetPasswordFromDtoAsync(new(id, "x")), check, "DTO reset applies password policy");
      await forms.ResetPasswordFromDtoAsync(new(id, "ReplacementPassword123!"));
      await using (var scope = provider.CreateAsyncScope()) {
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var reset = (await manager.FindByIdAsync(id))!;
        check(await manager.CheckPasswordAsync(reset, "ReplacementPassword123!") && reset.MustChangePassword && reset.SecurityStamp != saved.SecurityStamp,
          "DTO reset updates password, requires replacement and invalidates sessions");
      }
      await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountBlockReason, "Administrator"));
      await PostgresTests.ThrowsAsync<UserSecurityException>(() => forms.ResetPasswordFromDtoAsync(new(id, "AnotherPassword123!")), check, "DTO reset rejects blocked account");
      permissions.Value = new("root", false, []); access.Value = new("root", false, []);
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => forms.CreateFromDtoAsync(Create(prefix + "denied")), check, "DTO create requires Users.Manage");
      permissions.Value = new("manager", false, [Guard.Core.Identity.Permissions.Users.Read, Guard.Core.Identity.Permissions.Users.Manage]);
      access.Value = new("manager", false, []);
      await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => forms.CreateFromDtoAsync(Create(prefix + "outside")), check, "DTO create cannot bypass server employee scope");
      check(!Create(prefix).ToString()!.Contains("InitialPassword") && !new ResetUserPasswordCommand(id, "Secret").ToString()!.Contains("Secret"),
        "command diagnostics do not reveal passwords");
    }
    finally {
      await db.Users.Where(u => u.PersonalId == person.Id).ExecuteDeleteAsync();
      await db.Personals.Where(p => p.Id == person.Id).ExecuteDeleteAsync();
    }
  }
  private sealed class CallbackValidator : IUserValidator<ApplicationUser>
  {
    public Action? OnValidate;
    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user) {
      OnValidate?.Invoke(); return Task.FromResult(IdentityResult.Success);
    }
  }
  private sealed class MutablePermissions : IPermissionService
  {
    public UserAccessSnapshot Value = new("root", true, []);
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(Value);
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string permission, CancellationToken ct = default) => Task.FromResult(Value.Has(permission));
    public Task RequireAsync(string permission, CancellationToken ct = default) => Value.Has(permission) ? Task.CompletedTask : throw new UnauthorizedAccessException();
  }
  private sealed class MutableScope : IDataAccessScopeService
  {
    public DataAccessScope Value = new("root", true, []);
    public Task<DataAccessScope> GetAsync(CancellationToken ct = default) => Task.FromResult(Value);
  }
  private sealed class Audit : IAuditService
  {
    public List<(AuditEventType, string?, string?)> Events = [];
    public void LogIdentityEvent(AuditEventType type, string? user, string? ip = null, string? details = null) => Events.Add((type, user, details));
  }
}
