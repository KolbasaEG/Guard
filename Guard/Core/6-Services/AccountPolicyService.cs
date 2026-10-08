using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public class AccountPolicyService(IDbContextFactory<ApplicationDbContext> factory,
    IServiceProvider services, IAuditService audit, IAccessChangeNotifier notifier) : IAccountPolicyService
{
  public async Task<AccountPolicy> GetAsync(CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    return await db.Set<AccountPolicy>().AsNoTracking().SingleOrDefaultAsync(ct) ?? new AccountPolicy();
  }

  public async Task SaveAsync(AccountPolicy policy, CancellationToken ct = default)
  {
    // Разрешаем зависимость только при записи, сохраняя AuthenticationState текущего circuit.
    var permissions = services.GetRequiredService<IPermissionService>();
    var actor = await permissions.GetCurrentAsync(ct);
    if (!actor.IsRoot) throw new UnauthorizedAccessException("Политику изменяет только Root.");
    if (!AccountPolicyRules.Valid(policy)) throw new ArgumentException("Проверьте периоды (1–3650 дней), длину (6–100) и число различных символов.");
    await using var db = await factory.CreateDbContextAsync(ct);
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)", ct);
    actor = await permissions.GetCurrentAsync(ct);
    if (!actor.IsRoot) throw new UnauthorizedAccessException("Политику изменяет только Root.");
    var existing = await db.Set<AccountPolicy>().SingleOrDefaultAsync(ct);
    if (existing != null && existing.Version != policy.Version)
      throw new AccountPolicyConflictException();
    var now = DateTimeOffset.UtcNow;
    var passwordEnabledAt = policy.PasswordExpirationEnabled
      ? existing?.PasswordExpirationEnabled == true ? existing.PasswordEnabledAtUtc : now : null;
    var inactivityEnabledAt = policy.InactivityEnabled
      ? existing?.InactivityEnabled == true ? existing.InactivityEnabledAtUtc : now : null;
    var saved = existing ?? new AccountPolicy();
    db.Entry(saved).CurrentValues.SetValues(policy);
    saved.PasswordEnabledAtUtc = passwordEnabledAt;
    saved.InactivityEnabledAtUtc = inactivityEnabledAt;
    saved.Version = Guid.NewGuid();
    if (existing == null) db.Add(saved);
    await db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);
    policy.Version = saved.Version;
    policy.PasswordEnabledAtUtc = saved.PasswordEnabledAtUtc;
    policy.InactivityEnabledAtUtc = saved.InactivityEnabledAtUtc;
    audit.LogIdentityEvent(AuditEventType.AccountPolicyChanged, actor.UserId, details: $"Version={policy.Version}");
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
    if (AccountPolicyRules.Inactive(policy, user, root, now))
    {
      // Повторная проверка под тем же замком, что у административных операций.
      await using var tx = await db.Database.BeginTransactionAsync(ct);
      var acquired = await db.Database.SqlQueryRaw<bool>("SELECT pg_try_advisory_xact_lock(74162002) AS \"Value\"").SingleAsync(ct);
      if (!acquired) return new(true, false);
      user = await db.Users.SingleAsync(u => u.Id == userId, ct);
      policy = await db.Set<AccountPolicy>().AsNoTracking().SingleOrDefaultAsync(ct) ?? new();
      root = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
        where ur.UserId == userId && role.NormalizedName == "ROOT" select ur.UserId).AnyAsync(ct);
      if (user.AccountBlockReason == null && AccountPolicyRules.Inactive(policy, user, root, now))
      {
        user.AccountBlockReason = "Inactivity";
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        audit.LogIdentityEvent(AuditEventType.UserBlocked, userId, details: "Reason=Inactivity");
        // Уведомление выполняет фоновая проверка, чтобы не вызвать рекурсивную проверку circuit.
        return new(true, false);
      }
      await tx.CommitAsync(ct);
    }
    return new(user.AccountBlockReason != null || user.LockoutEnabled && user.LockoutEnd > now,
      AccountPolicyRules.PasswordExpired(policy, user, now));
  }

  public async Task RecordActivityAsync(string userId, CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    var now = DateTimeOffset.UtcNow;
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)", ct);
    await db.Users.Where(u => u.Id == userId && u.AccountBlockReason == null &&
      (!u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= now) &&
      (u.LastActivityAtUtc == null || u.LastActivityAtUtc < now.AddMinutes(-5)))
      .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastActivityAtUtc, now), ct);
    await tx.CommitAsync(ct);
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
