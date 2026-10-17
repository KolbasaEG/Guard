using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;

internal static class UserAdministrationIntegrationTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    var validator = new CallbackValidator();
    var creationFault = new CreationSaveFault();
    await using var optionsSource = await factory.CreateDbContextAsync();
    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
      .UseNpgsql(optionsSource.Database.GetConnectionString()).AddInterceptors(creationFault).Options;
    var services = new ServiceCollection().AddLogging();
    services.AddScoped(_ => new ApplicationDbContext(options));
    services.AddDataProtection().UseEphemeralDataProtectionProvider();
    services.AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
    services.AddSingleton<IUserValidator<ApplicationUser>>(validator);
    await using var provider = services.BuildServiceProvider();
    var permissions = new MutablePermissions(); var access = new MutableScope(); var audit = new Audit();
    var service = new SecurityService(null!, permissions, access,
      provider.GetRequiredService<IServiceScopeFactory>(), audit, new AccessChangeNotifier(NullLogger<AccessChangeNotifier>.Instance), null!, NullLogger<SecurityService>.Instance);
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
      string? intermediateId = null;
      creationFault.AfterSave = async operationDb => {
        var intermediate = await operationDb.Users.AsNoTracking().SingleAsync(u => u.UserName == prefix);
        intermediateId = intermediate.Id;
        check(intermediate.PersonalId == person.Id && intermediate.PasswordHash != null,
          "DTO creation reaches SQL with employee binding and Identity password hash before injected failure");
        check(!await db.Users.AnyAsync(u => u.UserName == prefix),
          "independent connection cannot see uncommitted DTO creation");
        throw new InvalidOperationException("Тестовый отказ после сохранения Identity до commit.");
      };
      await PostgresTests.ThrowsAsync<InvalidOperationException>(() => forms.CreateFromDtoAsync(Create(prefix)), check,
        "DTO creation propagates failure after Identity SQL save");
      check(intermediateId != null && !await db.Users.AnyAsync(u => u.Id == intermediateId || u.UserName == prefix) && audit.Events.Count == 0,
        "failed DTO creation rolls back saved user and publishes no success audit");
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
    await CheckCompletionAsync(provider, factory, check);
  }
  private static async Task CheckCompletionAsync(IServiceProvider provider, IDbContextFactory<ApplicationDbContext> factory,
      Action<bool, string> check)
  {
    foreach (var failure in new[] { "none", "audit", "notification", "both", "cancelled" }) {
      var delivery = new Delivery(failure);
      var service = new SecurityService(null!, new MutablePermissions(), new MutableScope(),
        provider.GetRequiredService<IServiceScopeFactory>(), delivery, delivery, null!, delivery);
      var name = "completion-user-" + Guid.NewGuid().ToString("N");
      await using var db = await factory.CreateDbContextAsync();
      var person = new Personal { FirstName = "Completion", LastName = "Test", CreatedBy = "root" };
      db.Personals.Add(person); await db.SaveChangesAsync();
      try {
        delivery.Observe = async () => await db.Users.AsNoTracking().AnyAsync(u => u.UserName == name);
        var id = await service.CreateFromDtoAsync(new(name, name + "@example.test", "InitialPassword123!", person.Id));
        delivery.Assert(check, AuditEventType.UserCreated, id);

        var edit = (await service.GetUserEditAsync(id)).User;
        var originalStamp = (await db.Users.AsNoTracking().SingleAsync(u => u.Id == id)).SecurityStamp;
        edit.Email = name + "changed@example.test";
        delivery.Reset(async () => await db.Users.AsNoTracking().AnyAsync(u => u.Id == id && u.Email == edit.Email && u.SecurityStamp != originalStamp));
        await service.SaveDetailsAsync(edit);
        delivery.Assert(check, AuditEventType.UserDetailsChanged, id);

        delivery.Reset(() => Task.FromResult(true));
        await PostgresTests.ThrowsAsync<InvalidOperationException>(() => service.SaveDetailsAsync(edit), check, "stale user edit still fails before completion");
        check(delivery.AuditCalls == 0 && delivery.NotificationCalls == 0 && delivery.Errors.Count == 0, "rejected user edit publishes no completion events");
        await PostgresTests.ThrowsAsync<UserSecurityException>(() => service.ResetPasswordFromDtoAsync(new(id, "x")), check, "password validation failure still propagates");
        check(delivery.AuditCalls == 0 && delivery.NotificationCalls == 0, "invalid reset publishes no completion events");

        delivery.Reset(async () => {
          await using var scope = provider.CreateAsyncScope();
          var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
          var saved = (await manager.FindByIdAsync(id))!;
          return saved.MustChangePassword && await manager.CheckPasswordAsync(saved, "ReplacementPassword123!");
        });
        await service.ResetPasswordFromDtoAsync(new(id, "ReplacementPassword123!"));
        delivery.Assert(check, AuditEventType.PasswordChanged, id);

        delivery.Reset(async () => await db.Users.AsNoTracking().AnyAsync(u => u.Id == id && u.AccountBlockReason == "Administrator" && u.LockoutEnd > DateTimeOffset.UtcNow));
        await service.SetUserLockoutAsync(id, true);
        delivery.Assert(check, AuditEventType.UserBlocked, id);

        delivery.Reset(async () => await db.Users.AsNoTracking().AnyAsync(u => u.Id == id && u.AccountBlockReason == null && u.LockoutEnd == null && u.UnblockedAtUtc != null));
        await service.SetUserLockoutAsync(id, false);
        delivery.Assert(check, AuditEventType.UserUnblocked, id);
      }
      finally {
        await db.Users.Where(u => u.PersonalId == person.Id).ExecuteDeleteAsync();
        await db.Personals.Where(p => p.Id == person.Id).ExecuteDeleteAsync();
      }
    }
  }
  private sealed class Delivery(string failure) : IAuditService, IAccessChangeNotifier, ILogger<SecurityService>
  {
    public Func<Task<bool>> Observe = () => Task.FromResult(false);
    public int NotificationCalls, AuditCalls;
    private bool notificationSawCommit, auditSawCommit;
    private string[] targets = [];
    private string? actor, details;
    private AuditEventType eventType;
    public List<Exception?> Errors { get; } = [];
    public void Reset(Func<Task<bool>> observe) {
      Observe = observe; NotificationCalls = AuditCalls = 0;
      notificationSawCommit = auditSawCommit = false; Errors.Clear(); targets = [];
    }
    public async Task PublishAsync(IEnumerable<string> userIds) {
      NotificationCalls++; targets = userIds.ToArray(); notificationSawCommit = await Observe();
      if (failure == "cancelled") throw new OperationCanceledException("Synthetic notification cancellation after commit");
      if (failure is "notification" or "both") throw new IOException("Synthetic notification failure");
    }
    public void LogIdentityEvent(AuditEventType type, string? user, string? ip = null, string? text = null) {
      AuditCalls++; actor = user; details = text; eventType = type; auditSawCommit = Observe().GetAwaiter().GetResult();
      if (failure == "cancelled") throw new OperationCanceledException("Synthetic audit cancellation after commit");
      if (failure is "audit" or "both") throw new IOException("Synthetic audit failure");
    }
    public void Assert(Action<bool, string> check, AuditEventType expectedType, string id) {
      check(AuditCalls == 1 && NotificationCalls == 1, $"{expectedType}: both deliveries attempted once with {failure}");
      check(notificationSawCommit && auditSawCommit, $"{expectedType}: independent context sees committed user write with {failure}");
      check(actor == "root" && eventType == expectedType && details == "Target=" + id && targets.SequenceEqual([id]), "completion retains trusted actor, event and affected user");
      var expectedErrors = failure switch { "none" => 0, "both" or "cancelled" => 2, _ => 1 };
      check(Errors.Count == expectedErrors && Errors.All(e => e != null), "each post-commit user delivery failure is logged");
    }
    public IDisposable Subscribe(string userId, Func<Task> refresh) => throw new NotSupportedException();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
      if (logLevel >= LogLevel.Error) Errors.Add(exception);
    }
  }
  private sealed class CreationSaveFault : SaveChangesInterceptor
  {
    public Func<ApplicationDbContext, Task>? AfterSave;
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
      CancellationToken ct = default)
    {
      var callback = AfterSave;
      AfterSave = null;
      if (callback != null) await callback((ApplicationDbContext)eventData.Context!);
      return result;
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
