using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;

internal static class PermissionBoundaryTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    using var cancellation = new CancellationTokenSource();
    var token = cancellation.Token;
    var session = new Session();
    var reader = new Reader();
    var policies = new Policies();
    var audit = new Audit();
    var service = new PermissionService(session, reader, audit, policies);
    await Denied(() => service.GetCurrentAsync(token), "anonymous session is denied before storage");
    check(reader.StampReads == 0 && policies.Reads == 0, "anonymous access does not read account data");
    session.Value = new("actor", "cookie");
    foreach (var stamp in new string?[] { null, "revoked" }) {
      reader.Stamp = stamp;
      await Denied(() => service.GetCurrentAsync(token), "missing or revoked stamp denies current session");
    }
    check(policies.Reads == 0, "invalid stamp stops before policy and role reads");
    reader.Stamp = "cookie";
    foreach (var state in new[] { new AccountState(true, false), new AccountState(false, true) }) {
      policies.State = state;
      await Denied(() => service.GetCurrentAsync(token), "block or expired password denies valid session");
    }
    check(reader.AccessReads == 0, "policy denial stops before role reads");
    policies.State = new(false, false);
    await Denied(() => service.GetCurrentAsync(token), "unavailable account denies valid session");
    reader.Data = new(["Root", "ADMINISTRATOR"], [Permissions.Personals.Export, Permissions.Roles.Manage, "invented", null]);
    var access = await service.GetCurrentAsync(token);
    check(!access.IsRoot && !access.Has(Permissions.Personals.Export) && !access.Has(Permissions.Roles.Manage),
      "role names cannot grant Root, dependencies and Root-only restrictions remain enforced");
    check(access.Permissions.SetEquals([Permissions.Personals.Export, Permissions.Roles.Manage]), "unknown and null role codes are removed");
    reader.Data = new([], [Permissions.Personals.Read, Permissions.Personals.Read, Permissions.Personals.Export, "personals.read"]);
    access = await service.GetCurrentAsync(token);
    check(access.Has(Permissions.Personals.Export) && access.Permissions.Count == 2 &&
      access.Permissions.Comparer.Equals(StringComparer.Ordinal), "known role permissions are distinct, ordinal and respect dependencies");
    reader.Data = new(["ROOT"], []);
    check((await service.GetCurrentAsync(token)).Has(Permissions.Roles.Manage) &&
      !await service.HasAsync("invented", token), "Root grants known catalog permissions only");
    var sessionReads = session.Reads;
    var stampReads = reader.StampReads;
    session.Value = null;
    check((await service.GetForUserAsync("background", token)).UserId == "background" &&
      reader.UserId == "background" && policies.UserId == "background", "background check uses saved author");
    check(session.Reads == sessionReads && reader.StampReads == stampReads, "background check needs neither current session nor its stamp");
    policies.State = new(true, false);
    await Denied(() => service.GetForUserAsync("background", token), "Root background author cannot bypass account policy");
    policies.State = new(false, false);
    check(!await service.HasAsync(Permissions.Personals.Read, token), "Has converts authorization denial to false");
    await Denied(() => service.RequireAsync(Permissions.Personals.Read, token), "Require denies anonymous access");
    check(audit.Count == 1 && audit.Actor == null && audit.Details == $"Permission={Permissions.Personals.Read}", "Require records denied permission");
    session.Value = new("actor", "cookie");
    reader.Failure = new InvalidOperationException("synthetic storage failure");
    try { await service.HasAsync(Permissions.Personals.Read, token); check(false, "storage failure propagates"); }
    catch (InvalidOperationException error) { check(ReferenceEquals(error, reader.Failure), "Has does not hide storage failure"); }
    reader.Failure = null;
    check(reader.Token == token && policies.Token == token && session.Token == token, "session, policy and storage receive cancellation token");
    cancellation.Cancel();
    try { await service.HasAsync(Permissions.Personals.Read, token); check(false, "cancellation propagates"); }
    catch (OperationCanceledException error) { check(error.CancellationToken == token, "Has does not hide cancellation"); }

    async Task Denied(Func<Task> action, string name) => await TestAssertions.ThrowsAsync<UnauthorizedAccessException>(action, check, name);
  }

  private sealed class Session : ICurrentSession
  {
    public CurrentSession? Value; public int Reads; public CancellationToken Token;
    public Task<CurrentSession?> GetAsync(CancellationToken ct = default) { Reads++; Token = ct; return Task.FromResult(Value); }
  }
  private sealed class Reader : IPermissionReader
  {
    public string? Stamp; public PermissionAccessData? Data; public Exception? Failure;
    public int StampReads; public int AccessReads; public string? UserId; public CancellationToken Token;
    public Task<string?> GetSecurityStampAsync(string userId, CancellationToken ct = default)
    {
      StampReads++; Token = ct; ct.ThrowIfCancellationRequested();
      if (Failure != null) throw Failure;
      return Task.FromResult(Stamp);
    }
    public Task<PermissionAccessData?> FindAccessAsync(string userId, CancellationToken ct = default)
    { AccessReads++; UserId = userId; Token = ct; ct.ThrowIfCancellationRequested(); return Task.FromResult(Data); }
  }
  private sealed class Policies : IAccountPolicyService
  {
    public AccountState State = new(false, false); public int Reads; public string? UserId; public CancellationToken Token;
    public Task<AccountState> CheckAsync(string userId, CancellationToken ct = default)
    { Reads++; UserId = userId; Token = ct; ct.ThrowIfCancellationRequested(); return Task.FromResult(State); }
    public Task<AccountLoginPolicyDto> GetLoginPolicyAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<AccountPasswordLifetimeDto> GetPasswordLifetimeAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task RecordActivityAsync(string userId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task SweepAsync(CancellationToken ct = default) => throw new NotSupportedException();
  }
  private sealed class Audit : IAuditService
  {
    public int Count; public string? Actor; public string? Details;
    public void LogIdentityEvent(AuditEventType type, string? userName, string? ipAddress = null, string? details = null)
    { if (type != AuditEventType.AccessDenied) throw new InvalidOperationException(); Count++; Actor = userName; Details = details; }
  }
}
