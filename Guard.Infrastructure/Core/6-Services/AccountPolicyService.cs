using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services.DTOs;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Guard.Core.Logging;
using System.Text.Json;

namespace Guard.Core.Services;

public class AccountPolicyService(IDbContextFactory<ApplicationDbContext> factory,
    IServiceProvider services, IAuditService audit, IAccessChangeNotifier notifier) : IAccountPolicyService, IPasswordRequirementsService, IAccountPolicyAdministrationService
{
  private static readonly Expression<Func<AccountPolicy, AccountPolicySettingsDto>> Settings = p => new() {
    Version = p.Version,
    MaxFailedLoginAttempts = p.MaxFailedLoginAttempts,
    LoginLockoutMinutes = p.LoginLockoutMinutes,
    PasswordExpirationEnabled = p.PasswordExpirationEnabled,
    PasswordDays = p.PasswordDays,
    InactivityEnabled = p.InactivityEnabled,
    InactivityDays = p.InactivityDays,
    MinimumLength = p.MinimumLength,
    UniqueCharacters = p.UniqueCharacters,
    RequireDigit = p.RequireDigit,
    RequireLowercase = p.RequireLowercase,
    RequireUppercase = p.RequireUppercase,
    RequireSymbol = p.RequireSymbol,
    PasswordEnabledAtUtc = p.PasswordEnabledAtUtc,
    InactivityEnabledAtUtc = p.InactivityEnabledAtUtc
  };
  private static readonly Func<AccountPolicy, AccountPolicySettingsDto> Snapshot = Settings.Compile();

  public async Task<PasswordRequirementsDto> GetRequirementsAsync(CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    return await db.Set<AccountPolicy>().AsNoTracking().Select(p => new PasswordRequirementsDto(
      p.MinimumLength, p.UniqueCharacters, p.RequireDigit, p.RequireLowercase, p.RequireUppercase, p.RequireSymbol))
      .SingleOrDefaultAsync(ct) ?? new();
  }

  public async Task<AccountLoginPolicyDto> GetLoginPolicyAsync(CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    return await db.Set<AccountPolicy>().AsNoTracking().Select(p => new AccountLoginPolicyDto(p.MaxFailedLoginAttempts, p.LoginLockoutMinutes))
      .SingleOrDefaultAsync(ct) ?? new();
  }

  public async Task<AccountPasswordLifetimeDto> GetPasswordLifetimeAsync(CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    return await db.Set<AccountPolicy>().AsNoTracking().Select(p => new AccountPasswordLifetimeDto(p.PasswordExpirationEnabled, p.PasswordDays, p.PasswordEnabledAtUtc))
      .SingleOrDefaultAsync(ct) ?? new();
  }

  private async Task<UserAccessSnapshot> RequireRootAsync(CancellationToken ct)
  {
    var actor = await services.GetRequiredService<IPermissionService>().GetCurrentAsync(ct);
    if (!actor.IsRoot) throw new UnauthorizedAccessException("Политика доступна только Root.");
    return actor;
  }

  public async Task<AccountPolicySettingsDto> GetSettingsAsync(CancellationToken ct = default)
  {
    var actor = await RequireRootAsync(ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    var result = await db.Set<AccountPolicy>().AsNoTracking().Select(Settings).SingleOrDefaultAsync(ct) ?? new();
    if ((await RequireRootAsync(ct)).UserId != actor.UserId) throw new UnauthorizedAccessException("Сессия изменилась. Повторите загрузку.");
    return result;
  }

  private static void Apply(SaveAccountPolicyCommand command, AccountPolicy policy)
  {
    policy.MaxFailedLoginAttempts = command.MaxFailedLoginAttempts;
    policy.LoginLockoutMinutes = command.LoginLockoutMinutes;
    policy.PasswordExpirationEnabled = command.PasswordExpirationEnabled;
    policy.PasswordDays = command.PasswordDays;
    policy.InactivityEnabled = command.InactivityEnabled;
    policy.InactivityDays = command.InactivityDays;
    policy.MinimumLength = command.MinimumLength;
    policy.UniqueCharacters = command.UniqueCharacters;
    policy.RequireDigit = command.RequireDigit;
    policy.RequireLowercase = command.RequireLowercase;
    policy.RequireUppercase = command.RequireUppercase;
    policy.RequireSymbol = command.RequireSymbol;
  }

  public Task<AccountPolicySettingsDto> SaveAsync(SaveAccountPolicyCommand command, CancellationToken ct = default) =>
    AuditOperation.RunAsync(audit, Guard.Core.Enums.AuditEventType.AccountPolicyChanged, "AccountPolicy", "1", () => SaveCoreAsync(command, ct));
  private async Task<AccountPolicySettingsDto> SaveCoreAsync(SaveAccountPolicyCommand command, CancellationToken ct)
  {
    var actor = await RequireRootAsync(ct);
    ArgumentNullException.ThrowIfNull(command);
    var policy = new AccountPolicy();
    Apply(command, policy);
    if (!AccountPolicyRules.Valid(policy)) throw new ArgumentException("Проверьте число попыток входа (1–100), блокировку (1–10080 минут), периоды (1–3650 дней), длину пароля (6–100) и число различных символов.");
    await using var db = await factory.CreateDbContextAsync(ct);
    db.AuditActorId = actor.UserId;
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)", ct);
    if ((await RequireRootAsync(ct)).UserId != actor.UserId) throw new UnauthorizedAccessException("Сессия изменилась. Повторите сохранение.");
    var existing = await db.Set<AccountPolicy>().SingleOrDefaultAsync(ct);
    var before = existing == null ? null : Snapshot(existing);
    if (existing != null && existing.Version != command.Version)
      throw new AccountPolicyConflictException();
    var now = DateTimeOffset.UtcNow;
    var passwordEnabledAt = policy.PasswordExpirationEnabled
      ? existing?.PasswordExpirationEnabled == true ? existing.PasswordEnabledAtUtc : now : null;
    var inactivityEnabledAt = policy.InactivityEnabled
      ? existing?.InactivityEnabled == true ? existing.InactivityEnabledAtUtc : now : null;
    var saved = existing ?? new AccountPolicy();
    Apply(command, saved);
    saved.PasswordEnabledAtUtc = passwordEnabledAt;
    saved.InactivityEnabledAtUtc = inactivityEnabledAt;
    saved.Version = Guid.NewGuid();
    if (existing == null) db.Add(saved);
    await db.SaveChangesAsync(ct);
    await AuditTransaction.RecordAsync(db, audit, AuditEventData.Create(AuditEventType.AccountPolicyChanged, actor.UserId,
      details: JsonSerializer.Serialize(new { Before = before, After = Snapshot(saved) }))
      with { ObjectType = "AccountPolicy", ObjectId = "1" }, ct);
    await AuditTransaction.CommitAsync(tx, ct);
    return Snapshot(saved);
  }

  public async Task<AccountState> CheckAsync(string userId, CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct);
    if (user == null) return new(true, false);
    var now = DateTimeOffset.UtcNow;
    if (user.AccountBlockReason != null || user.LockoutEnabled && user.LockoutEnd > now) return new(true, false);
    var policy = await db.Set<AccountPolicy>().AsNoTracking().SingleOrDefaultAsync(ct) ?? new();
    var root = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
      where ur.UserId == userId && role.NormalizedName == "ROOT" select ur.UserId).AnyAsync(ct);
    if (AccountPolicyRules.Inactive(policy, user.ToAccountPolicyState(), root, now))
    {
      try { if (services.GetService<IOperationAvailability>() is { } available) await available.RequireWorkingAsync(ct); }
      catch (AuditUnavailableException) { return new(true, false); }
      // Повторная проверка под тем же замком, что у административных операций.
      await using var tx = await db.Database.BeginTransactionAsync(ct);
      var acquired = await db.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_xact_lock(74162002) AS \"Value\"").SingleAsync(ct);
      if (!acquired) return new(true, false);
      user = await db.Users.SingleAsync(u => u.Id == userId, ct);
      policy = await db.Set<AccountPolicy>().AsNoTracking().SingleOrDefaultAsync(ct) ?? new();
      root = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
        where ur.UserId == userId && role.NormalizedName == "ROOT" select ur.UserId).AnyAsync(ct);
      if (user.AccountBlockReason == null && AccountPolicyRules.Inactive(policy, user.ToAccountPolicyState(), root, now))
      {
        user.AccountBlockReason = "Inactivity";
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        await db.SaveChangesAsync(ct);
        await AuditTransaction.RecordAsync(db, audit, AuditEventData.Create(AuditEventType.UserBlocked, null,
          details: "Reason=Inactivity") with { Actor = null, ActorKind = "system", ObjectType = "Account", ObjectId = userId, ReasonCode = "Inactivity" }, ct);
        await AuditTransaction.CommitAsync(tx, ct);
        // Уведомление выполняет фоновая проверка, чтобы не вызвать рекурсивную проверку circuit.
        return new(true, false);
      }
      await AuditTransaction.CommitAsync(tx, ct);
    }
    return new(user.AccountBlockReason != null || user.LockoutEnabled && user.LockoutEnd > now,
      AccountPolicyRules.PasswordExpired(policy, user.ToAccountPolicyState(), now));
  }

  public async Task RecordActivityAsync(string userId, CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    var now = DateTimeOffset.UtcNow;
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)", ct);
    if (db.AuditWriter != null) await db.AuditWriter.RequireWorkingTransactionAsync(db, ct);
    await db.Users.Where(u => u.Id == userId && u.AccountBlockReason == null &&
      (!u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= now) &&
      (u.LastActivityAtUtc == null || u.LastActivityAtUtc < now.AddMinutes(-5)))
      .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastActivityAtUtc, now), ct);
    await AuditTransaction.CommitAsync(tx, ct);
  }

  public async Task SweepAsync(CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    var policy = await db.Set<AccountPolicy>().AsNoTracking().SingleOrDefaultAsync(ct) ?? new();
    var now = DateTimeOffset.UtcNow;
    var passwordCutoff = now.AddDays(-policy.PasswordDays);
    var inactivityCutoff = now.AddDays(-policy.InactivityDays);
    var ids = await db.Users.Where(u => u.AccountBlockReason != null || u.MustChangePassword ||
      policy.PasswordExpirationEnabled && (u.PasswordChangedAtUtc != null ? u.PasswordChangedAtUtc <= passwordCutoff :
        u.CreatedAtUtc <= passwordCutoff && (policy.PasswordEnabledAtUtc == null || policy.PasswordEnabledAtUtc <= passwordCutoff)) ||
      policy.InactivityEnabled && (policy.InactivityEnabledAtUtc == null || policy.InactivityEnabledAtUtc <= inactivityCutoff) &&
        (u.LastActivityAtUtc ?? u.CreatedAtUtc) <= inactivityCutoff && (u.UnblockedAtUtc == null || u.UnblockedAtUtc <= inactivityCutoff))
      .Select(u => u.Id).ToListAsync(ct);
    foreach (var id in ids) { ct.ThrowIfCancellationRequested(); await CheckAsync(id, ct); }
    await notifier.PublishAsync(ids);
  }
}
