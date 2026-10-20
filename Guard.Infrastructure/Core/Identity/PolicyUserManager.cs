using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Guard.Core.Contexts;
using Guard.Core.Logging;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Identity;

public class PolicyUserManager(IUserStore<ApplicationUser> store, IOptions<IdentityOptions> options,
    IPasswordHasher<ApplicationUser> hasher, IEnumerable<IUserValidator<ApplicationUser>> users,
    IEnumerable<IPasswordValidator<ApplicationUser>> passwords, ILookupNormalizer normalizer,
    IdentityErrorDescriber errors, IServiceProvider services, ILogger<UserManager<ApplicationUser>> logger,
    IAuditService audit)
    : UserManager<ApplicationUser>(store, LocalOptions(options.Value), hasher, users, passwords, normalizer, errors, services, logger)
{
  // IOptions.Value is shared. Keep the dynamically changed Lockout options local to this manager.
  private static IOptions<IdentityOptions> LocalOptions(IdentityOptions shared) => Microsoft.Extensions.Options.Options.Create(new IdentityOptions {
    ClaimsIdentity = shared.ClaimsIdentity, User = shared.User, Password = shared.Password,
    SignIn = shared.SignIn, Tokens = shared.Tokens, Stores = shared.Stores,
    Lockout = new LockoutOptions {
      AllowedForNewUsers = shared.Lockout.AllowedForNewUsers,
      MaxFailedAccessAttempts = shared.Lockout.MaxFailedAccessAttempts,
      DefaultLockoutTimeSpan = shared.Lockout.DefaultLockoutTimeSpan
    }
  });

  public override async Task<IdentityResult> AccessFailedAsync(ApplicationUser user)
  {
    var ct = ServiceProvider.GetService<IHttpContextAccessor>()?.HttpContext?.RequestAborted ?? default;
    var policy = await ServiceProvider.GetRequiredService<IAccountPolicyService>().GetLoginPolicyAsync(ct);
    if (policy.MaxFailedLoginAttempts is < 1 or > 100 || policy.LoginLockoutMinutes is < 1 or > 10080) throw new InvalidOperationException("Некорректная политика блокировки входа.");
    Options.Lockout.MaxFailedAccessAttempts = policy.MaxFailedLoginAttempts;
    Options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(policy.LoginLockoutMinutes);
    return await AuditedAsync(user, () => base.AccessFailedAsync(user),
      () => user.LockoutEnd > DateTimeOffset.UtcNow ? AuditEventType.UserBlocked : AuditEventType.UserDetailsChanged,
      "Failed login counter; temporary lockout policy", ct, AuditEventType.UserDetailsChanged);
  }

  private async Task<IdentityResult> SetPasswordAsync(ApplicationUser user, string password, Func<Task<IdentityResult>> change)
  {
    if (user.AccountBlockReason != null || await IsLockedOutAsync(user))
      return IdentityResult.Failed(new IdentityError { Code = "AccountBlocked", Description = "Учётная запись заблокирована." });
    if (user.PasswordHash != null && PasswordHasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed)
      return IdentityResult.Failed(new IdentityError { Code = "PasswordUnchanged", Description = "Новый пароль должен отличаться от текущего." });
    var previous = (user.PasswordChangedAtUtc, user.MustChangePassword, user.PasswordHash, user.SecurityStamp);
    user.PasswordChangedAtUtc = DateTimeOffset.UtcNow;
    user.MustChangePassword = false;
    var succeeded = false;
    try {
      var result = await change();
      succeeded = result.Succeeded;
      return result;
    }
    finally {
      if (!succeeded) (user.PasswordChangedAtUtc, user.MustChangePassword, user.PasswordHash, user.SecurityStamp) = previous;
    }
  }

  public override Task<IdentityResult> CreateAsync(ApplicationUser user, string password) =>
    SetPasswordAsync(user, password, () => base.CreateAsync(user, password));

  public override async Task<IdentityResult> AddPasswordAsync(ApplicationUser user, string password)
  {
    return await AuditedAsync(user, () => SetPasswordAsync(user, password, () => base.AddPasswordAsync(user, password)),
      () => AuditEventType.PasswordChanged, "Password added", RequestToken);
  }

  public override async Task<IdentityResult> ResetPasswordAsync(ApplicationUser user, string token, string newPassword)
  {
    if (!await VerifyUserTokenAsync(user, Options.Tokens.PasswordResetTokenProvider, ResetPasswordTokenPurpose, token))
      return IdentityResult.Failed(ErrorDescriber.InvalidToken());
    return await AuditedAsync(user, () => SetPasswordAsync(user, newPassword, () => base.ResetPasswordAsync(user, token, newPassword)),
      () => AuditEventType.PasswordChanged, "Administrative password reset", RequestToken);
  }

  public override async Task<IdentityResult> ChangePasswordAsync(ApplicationUser user, string currentPassword, string newPassword)
  {
    return await AuditedAsync(user, async () => {
      if (!await CheckPasswordAsync(user, currentPassword))
        return IdentityResult.Failed(new IdentityError { Code = "PasswordMismatch", Description = "Текущий пароль неверен." });
      return await SetPasswordAsync(user, newPassword, () => base.ChangePasswordAsync(user, currentPassword, newPassword));
    }, () => AuditEventType.PasswordChanged, "Own password change", RequestToken);
  }

  private CancellationToken RequestToken => ServiceProvider.GetService<IHttpContextAccessor>()?.HttpContext?.RequestAborted ?? default;

  private async Task<IdentityResult> AuditedAsync(ApplicationUser user, Func<Task<IdentityResult>> action,
    Func<AuditEventType> type, string details, CancellationToken ct, AuditEventType failureType = AuditEventType.PasswordChangeFailed)
  {
    var db = ServiceProvider.GetService<ApplicationDbContext>();
    var own = db != null && db.Database.IsRelational() && db.Database.CurrentTransaction == null;
    await using var tx = own ? await db!.Database.BeginTransactionAsync(ct) : null;
    var original = db?.Entry(user).CurrentValues.Clone();
    try {
      var result = await action();
      var initiator = AuditOperationContext.Current?.UserId ?? db?.AuditActorId;
      var entry = AuditEventData.ForAccount(result.Succeeded ? type() : failureType, user.Id,
        details: details) with { Actor = initiator, ActorKind = initiator != null ? "user" : AuditOperationContext.Current?.ActorKind ?? "system",
        Outcome = result.Succeeded ? "success" : "failure",
        ReasonCode = result.Succeeded ? null : string.Join(",", result.Errors.Select(e => e.Code)) };
      if (db?.Database.CurrentTransaction != null) await AuditTransaction.RecordAsync(db, audit, entry, ct);
      else await audit.RecordAsync(entry, ct);
      if (tx != null) await AuditTransaction.CommitAsync(tx, ct);
      return result;
    }
    catch (OperationCommitUnknownException) { throw; }
    catch {
      if (tx != null) await tx.RollbackAsync(CancellationToken.None);
      if (db != null && original != null) { db.Entry(user).CurrentValues.SetValues(original); db.Entry(user).OriginalValues.SetValues(original); }
      throw;
    }
  }
}
