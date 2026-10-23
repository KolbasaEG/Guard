using System.Reflection;
using System.Text.Json;
using Guard.Components.Pages.AccountPolicy;
using Guard.Core.Contexts;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

internal static class AccountPolicyBoundaryTests
{
  public static async Task CheckIntegrationAsync(IServiceProvider services, IDbContextFactory<ApplicationDbContext> factory, Action<bool, string> check)
  {
    var administration = services.GetRequiredService<IAccountPolicyAdministrationService>();
    var runtime = services.GetRequiredService<IAccountPolicyService>();
    var original = await administration.GetSettingsAsync();
    try
    {
      var command = original.ToCommand() with { MaxFailedLoginAttempts = 9, LoginLockoutMinutes = 31,
        PasswordExpirationEnabled = true, PasswordDays = 71, InactivityEnabled = true, InactivityDays = 83,
        MinimumLength = 13, UniqueCharacters = 7, RequireDigit = false, RequireLowercase = true,
        RequireUppercase = false, RequireSymbol = true };
      var before = DateTimeOffset.UtcNow;
      var saved = await administration.SaveAsync(command);
      check(saved.ToCommand() == command with { Version = saved.Version } && saved.Version != command.Version &&
        command.Version == original.Version, "policy save returns all edited fields and a new version without mutating its command");
      check(saved.PasswordEnabledAtUtc.HasValue && saved.InactivityEnabledAtUtc.HasValue &&
        (original.PasswordExpirationEnabled || saved.PasswordEnabledAtUtc >= before) &&
        (original.InactivityEnabled || saved.InactivityEnabledAtUtc >= before) &&
        saved.PasswordEnabledAtUtc.Value.Offset == TimeSpan.Zero && saved.InactivityEnabledAtUtc.Value.Offset == TimeSpan.Zero,
        "policy enable dates are assigned by the server in UTC");
      var persisted = await administration.GetSettingsAsync();
      check(AccountPolicyForm.From(persisted).ToCommand(persisted.Version) == persisted.ToCommand(),
        "policy form preserves every editable field and loaded version");
      saved = await administration.SaveAsync(persisted.ToCommand() with { PasswordDays = 72 });
      check(saved.PasswordEnabledAtUtc == persisted.PasswordEnabledAtUtc && saved.InactivityEnabledAtUtc == persisted.InactivityEnabledAtUtc,
        "changing enabled policy preserves both activation dates");
      var login = await runtime.GetLoginPolicyAsync();
      var lifetime = await runtime.GetPasswordLifetimeAsync();
      check(login == new AccountLoginPolicyDto(9, 31) && lifetime == new AccountPasswordLifetimeDto(true, 72, persisted.PasswordEnabledAtUtc),
        "login and user-search readers receive their narrow persisted policy snapshots");
      await using (var publicProvider = new ServiceCollection().BuildServiceProvider())
      {
        var anonymous = new AccountPolicyService(factory, publicProvider, new AuditStub(), services.GetRequiredService<IAccessChangeNotifier>());
        var requirements = await anonymous.GetRequirementsAsync();
        var json = JsonSerializer.Serialize(requirements);
        check(requirements == new PasswordRequirementsDto(13, 7, false, true, false, true) &&
          !json.Contains("Version") && !json.Contains("PasswordDays") && !json.Contains("Inactivity") && !json.Contains("Lockout"),
          "anonymous password requirements expose only complexity fields without resolving authentication services");
        check(PolicyPasswordValidator.Validate(requirements, "longpassword!").Succeeded &&
          !PolicyPasswordValidator.Validate(requirements, "short!").Succeeded,
          "password validation consumes the same public requirements DTO as the hint");
      }
      saved = await administration.SaveAsync(saved.ToCommand() with { PasswordExpirationEnabled = false, InactivityEnabled = false });
      check(saved.PasswordEnabledAtUtc == null && saved.InactivityEnabledAtUtc == null, "disabling policy clears activation dates");
      saved = await administration.SaveAsync(saved.ToCommand() with { PasswordExpirationEnabled = true, InactivityEnabled = true });
      check(saved.PasswordEnabledAtUtc >= persisted.PasswordEnabledAtUtc && saved.InactivityEnabledAtUtc >= persisted.InactivityEnabledAtUtc,
        "reenabling policy starts new server-owned periods");
      var current = saved;
      foreach (var changing in new[] { false, true })
      {
        var permissions = new ChangingPermissions(changing);
        await using var provider = new ServiceCollection().AddSingleton<IPermissionService>(permissions).BuildServiceProvider();
        var guarded = new AccountPolicyService(factory, provider, new AuditStub(), services.GetRequiredService<IAccessChangeNotifier>());
        await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => guarded.GetSettingsAsync(), check,
          "policy administrative read rejects loss of Root or actor replacement during read");
        permissions.Calls = 0;
        await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => guarded.SaveAsync(current.ToCommand() with { MinimumLength = 20 }), check,
          "policy save rejects loss of Root or actor replacement under the transaction lock");
      }
      check((await administration.GetSettingsAsync()).Version == current.Version, "rejected policy writes leave persisted version unchanged");
      using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => services.GetRequiredService<IPasswordRequirementsService>().GetRequirementsAsync(cancelled.Token),
        check, "public requirements forward cancellation to database");
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => administration.GetSettingsAsync(cancelled.Token),
        check, "administrative policy read forwards cancellation");
    }
    finally
    {
      var current = await administration.GetSettingsAsync();
      await administration.SaveAsync(original.ToCommand() with { Version = current.Version });
    }
  }

  public static async Task RunAsync(Action<bool, string> check)
  {
    check(typeof(SaveAccountPolicyCommand).GetProperty("Id") == null &&
      typeof(SaveAccountPolicyCommand).GetProperty("PasswordEnabledAtUtc") == null &&
      typeof(SaveAccountPolicyCommand).GetProperty("InactivityEnabledAtUtc") == null,
      "policy write contract cannot carry singleton ID or activation timestamps from UI");
    var service = DispatchProxy.Create<IAccountPolicyAdministrationService, IpAddressReferenceTests.ServiceProxy>();
    var proxy = (IpAddressReferenceTests.ServiceProxy)(object)service;
    using var page = new AccountPolicySettings();
    var notifications = new Radzen.NotificationService();
    typeof(AccountPolicySettings).GetProperty("Notifications", Flags)!.SetValue(page, notifications);
    void Rendered() => typeof(AccountPolicySettings).GetMethod("OnAfterRender", Flags)!.Invoke(page, [false]);
    typeof(AccountPolicySettings).GetProperty("Policies", Flags)!.SetValue(page, service);
    typeof(AccountPolicySettings).GetProperty("Logger", Flags)!.SetValue(page, NullLogger<AccountPolicySettings>.Instance);
    proxy.Handle = (_, _) => Task.FromException<AccountPolicySettingsDto>(new IOException());
    await Call(page, "OnInitializedAsync");
    Rendered(); Rendered();
    check(notifications.Messages.Count == 1 && notifications.Messages[0].Severity == Radzen.NotificationSeverity.Error &&
      notifications.Messages[0].Detail.Contains("Обновите настройки"),
      "failed policy load notifies once after rendering and explains recovery");
    var calls = 0;
    proxy.Handle = (_, _) => { calls++; return Task.FromResult(new AccountPolicySettingsDto()); };
    await Call(page, "SaveAsync");
    check(calls == 0, "policy form cannot save defaults after failed settings load");
    var version = Guid.NewGuid();
    proxy.Handle = (_, _) => Task.FromResult(new AccountPolicySettingsDto { Version = version, MinimumLength = 13 });
    await Call(page, "LoadAsync");
    var form = Get<AccountPolicyForm>(page, "model"); form.MinimumLength = 14;
    var completion = new TaskCompletionSource<AccountPolicySettingsDto>(TaskCreationOptions.RunContinuationsAsynchronously);
    SaveAccountPolicyCommand? command = null; CancellationToken token = default;
    proxy.Handle = (_, args) => { calls++; command = (SaveAccountPolicyCommand)args[0]!; token = (CancellationToken)args[1]!; return completion.Task; };
    var saving = Call(page, "SaveAsync"); await Call(page, "SaveAsync"); form.MinimumLength = 15;
    Rendered();
    check(notifications.Messages.Count == 1, "policy save does not announce success before the service completes");
    check(calls == 1 && command?.MinimumLength == 14 && command.Version == version,
      "policy form submits one immutable command with the loaded version");
    var newVersion = Guid.NewGuid();
    completion.SetResult(new() { Version = newVersion, MinimumLength = 14 }); await saving;
    Rendered(); Rendered();
    check(notifications.Messages.Count == 2 && notifications.Messages[1].Severity == Radzen.NotificationSeverity.Success &&
      notifications.Messages[1].Detail == "Настройки сохранены.",
      "committed policy save emits one success notification across repeated renders");
    check(Get<AccountPolicyForm>(page, "model").MinimumLength == 14 && Get<Guid>(page, "version") == newVersion,
      "policy form adopts committed values and version from the save result");
    proxy.Handle = (_, _) => Task.FromException<AccountPolicySettingsDto>(new AccountPolicyConflictException());
    await Call(page, "SaveAsync");
    Rendered();
    check(notifications.Messages.Count == 3 && notifications.Messages[2].Severity == Radzen.NotificationSeverity.Error &&
      notifications.Messages[2].Detail.Contains("другой сессии"),
      "policy conflict emits an error notification instead of success");
    check(!Get<bool>(page, "loaded"), "policy conflict requires reload before another save");
    proxy.Handle = (_, _) => Task.FromResult(new AccountPolicySettingsDto { Version = newVersion });
    await Call(page, "LoadAsync");
    completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    proxy.Handle = (_, args) => { token = (CancellationToken)args[1]!; return completion.Task; };
    saving = Call(page, "SaveAsync"); page.Dispose();
    completion.SetResult(new() { Version = Guid.NewGuid(), MinimumLength = 25 }); await saving;
    Rendered();
    check(notifications.Messages.Count == 3, "late policy save result does not notify after leaving the page");
    check(token.IsCancellationRequested && Get<Guid>(page, "version") == newVersion,
      "leaving policy page cancels pending save and ignores its late result");
    var requirements = DispatchProxy.Create<IPasswordRequirementsService, IpAddressReferenceTests.ServiceProxy>();
    var requirementsProxy = (IpAddressReferenceTests.ServiceProxy)(object)requirements;
    var pending = new TaskCompletionSource<PasswordRequirementsDto>(TaskCreationOptions.RunContinuationsAsynchronously);
    requirementsProxy.Handle = (_, args) => { token = (CancellationToken)args[0]!; return pending.Task; };
    using var hint = new Guard.Components.Library.PasswordRequirements();
    hint.GetType().GetProperty("Policies", Flags)!.SetValue(hint, requirements);
    var reading = Call(hint, "OnInitializedAsync"); hint.Dispose();
    pending.SetResult(new(20)); await reading;
    check(token.IsCancellationRequested && Get<PasswordRequirementsDto?>(hint, "policy") == null,
      "password hint cancels read and ignores late DTO after disposal");
  }

  private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
  private static T Get<T>(object page, string name) => (T)page.GetType().GetField(name, Flags)!.GetValue(page)!;
  private static Task Call(object page, string name) => (Task)page.GetType().GetMethod(name, Flags)!.Invoke(page, null)!;
  private sealed class ChangingPermissions(bool replaceActor) : IPermissionService
  {
    public int Calls;
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(++Calls == 1
      ? new UserAccessSnapshot("root", true, []) : new UserAccessSnapshot(replaceActor ? "another-root" : "root", replaceActor, []));
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> HasAsync(string permission, CancellationToken ct = default) => throw new NotSupportedException();
    public Task RequireAsync(string permission, CancellationToken ct = default) => throw new NotSupportedException();
  }
  private sealed class AuditStub : IAuditService
  {
    public Task RecordAsync(Guard.Core.Services.AuditEventData entry, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); LogIdentityEvent(entry.EventType, entry.Actor, entry.ClientIp, entry.Details); return Task.CompletedTask; }
    public void LogIdentityEvent(Guard.Core.Enums.AuditEventType type, string? userName, string? ipAddress = null, string? details = null) { }
  }
}
