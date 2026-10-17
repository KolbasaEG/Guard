
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace Guard.Core.Services;
public partial class SecurityService(
  IReadRepository<ApplicationUser> userReads, IPermissionService permissions,
  IDataAccessScopeService accessScopes, IServiceScopeFactory scopes,
  IAuditService audit, IAccessChangeNotifier notifier, IAccountPolicyService policies, ILogger<SecurityService> logger) : IUserAdministrationService, IUserListService
{
  public async Task<IReadOnlyList<Guard.Core.Services.DTOs.UserPersonalOption>> GetUserPersonalOptionsAsync(CancellationToken ct=default)
  {
    var initial = await RequireUserWriteAsync(ct);
    var access = initial.Scope;
    await using var scope = scopes.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var query = db.Set<Personal>().AsNoTracking().Where(p =>
      (p.Status == Status.Inserted || p.Status == Status.Modified) && !db.Users.Any(u => u.PersonalId == p.Id));
    if (!access.IsRoot) query = query.Where(p => p.SubdivisionId.HasValue && access.SubdivisionIds.Contains(p.SubdivisionId.Value));
    var options = await query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ThenBy(p => p.Id)
      .Select(p => new Guard.Core.Services.DTOs.UserPersonalOption(p.Id,
        p.LastName + " " + p.FirstName + " " + (p.MiddleName ?? ""))).ToListAsync(ct);
    await RecheckUserWriteAsync(initial, ct);
    return options;
  }
  private static void Check(IdentityResult result) {
    if (!result.Succeeded) throw new UserSecurityException(string.Join("; ", result.Errors.Select(e=>e.Description)));
  }
  public async Task<Guard.Core.Services.DTOs.UserEditData> GetUserEditAsync(string id, CancellationToken ct = default)
  {
    var initial = await RequireUserWriteAsync(ct);
    var access = initial.Scope;
    await using var scope = scopes.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct)
      ?? throw new KeyNotFoundException("Пользователь не найден.");
    await CheckTargetAsync(db, user, ct);
    if (user.AccountBlockReason != null || user.LockoutEnabled && user.LockoutEnd > DateTimeOffset.UtcNow)
      throw new InvalidOperationException("Сначала разблокируйте пользователя.");
    var query = db.Personals.AsNoTracking().Where(p =>
      ((p.Status == Status.Inserted || p.Status == Status.Modified) && !db.Users.Any(u => u.PersonalId == p.Id)) || p.Id == user.PersonalId);
    if (!access.IsRoot) query = query.Where(p => p.SubdivisionId.HasValue && access.SubdivisionIds.Contains(p.SubdivisionId.Value));
    var options = await query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
      .Select(p => new Guard.Core.Services.DTOs.UserPersonalOption(p.Id, p.LastName + " " + p.FirstName + " " + (p.MiddleName ?? ""))).ToListAsync(ct);
    await RecheckUserWriteAsync(initial, ct);
    return new(new() { Id = user.Id, UserName = user.UserName ?? "", Email = user.Email ?? "",
      PersonalId = user.PersonalId, Version = user.ConcurrencyStamp ?? "" }, options, access.IsRoot);
  }

  private Task<IdentityResult> UpdateUserDetailsAsync(Guard.Core.Services.DTOs.EditUserDto input, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(input);
    ValidateUserFields(input.UserName, input.Email);
    if (string.IsNullOrWhiteSpace(input.Id) || string.IsNullOrWhiteSpace(input.Version))
      throw new ArgumentException("Откройте форму пользователя заново.");
    return WriteAsync(async (db, manager) => {
      var user = await manager.FindByIdAsync(input.Id) ?? throw new KeyNotFoundException("Пользователь не найден.");
      await CheckTargetAsync(db, user, ct);
      if (user.AccountBlockReason != null || await manager.IsLockedOutAsync(user))
        throw new InvalidOperationException("Сначала разблокируйте пользователя.");
      if (user.ConcurrencyStamp != input.Version)
        throw new InvalidOperationException("Пользователь уже изменён. Закройте форму и откройте её заново.");
      await CheckPersonalAsync(db, input.PersonalId, ct);
      if (input.PersonalId.HasValue && await db.Users.AnyAsync(u => u.Id != input.Id && u.PersonalId == input.PersonalId, ct))
        throw new InvalidOperationException("У выбранного сотрудника уже есть учётная запись.");
      var email = input.Email.Trim();
      if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)) user.EmailConfirmed = false;
      user.UserName = input.UserName.Trim(); user.Email = email; user.PersonalId = input.PersonalId;
      Check(await manager.UpdateAsync(user));
      Check(await manager.UpdateSecurityStampAsync(user));
    }, ct, input.Id, AuditEventType.UserDetailsChanged);
  }
  private async Task CheckPersonalAsync(ApplicationDbContext db, Guid? id, CancellationToken ct)
  {
    var access = await accessScopes.GetAsync(ct);
    if (id == null) { if (!access.IsRoot) throw new UnauthorizedAccessException("Необходимо доступное подразделение сотрудника."); return; }
    var subdivision = await db.Set<Personal>().Where(p=>p.Id==id && (p.Status==Status.Inserted || p.Status==Status.Modified))
      .Select(p=>p.SubdivisionId).SingleOrDefaultAsync(ct);
    if (!access.Allows(subdivision) || !await db.Set<Personal>().AnyAsync(p=>p.Id==id && (p.Status==Status.Inserted || p.Status==Status.Modified),ct))
      throw new UnauthorizedAccessException("Сотрудник недоступен.");
  }
  private async Task CheckTargetAsync(ApplicationDbContext db, ApplicationUser user, CancellationToken ct)
  {
    var current = await permissions.GetCurrentAsync(ct);
    if (!current.IsRoot) {
      if (await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId==user.Id && r.NormalizedName=="ROOT" select ur).AnyAsync(ct))
        throw new UnauthorizedAccessException("Изменение Root доступно только Root.");
      await CheckPersonalAsync(db,user.PersonalId,ct);
    }
  }
  private async Task<IdentityResult> WriteAsync(Func<ApplicationDbContext,UserManager<ApplicationUser>,Task> write, CancellationToken ct,
    string? targetId = null, AuditEventType? eventType = null, Func<string?>? createdId = null)
  {
    var initial = await RequireUserWriteAsync(ct);
    var actorId = initial.Actor.UserId;
    await using var scope = scopes.CreateAsyncScope();
    var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await using var tx=await db.Database.BeginTransactionAsync(ct);
    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)",ct);
    await RecheckUserWriteAsync(initial, ct);
    await write(db,manager);
    await RecheckUserWriteAsync(initial, ct);
    ct.ThrowIfCancellationRequested();
    await tx.CommitAsync(ct);
    targetId ??= createdId?.Invoke();
    if (targetId != null) {
      // Commit is confirmed; delivery failures cannot undo the user write.
      if (eventType.HasValue) {
        try { audit.LogIdentityEvent(eventType.Value, actorId, details: $"Target={targetId}"); }
        catch (Exception ex) {
          logger.LogError(ex, "Изменение пользователя сохранено, но аудит не доставлен. EventType={EventType}; TargetId={TargetId}", eventType, targetId);
        }
      }
      try { await notifier.PublishAsync([targetId]); }
      catch (Exception ex) {
        logger.LogError(ex, "Изменение пользователя сохранено, но уведомление не доставлено. EventType={EventType}; TargetId={TargetId}", eventType, targetId);
      }
    }
    return IdentityResult.Success;
  }
  private async Task<IdentityResult> CreateUserAsync(ApplicationUser user,string password,CancellationToken ct=default)
  {
    ArgumentNullException.ThrowIfNull(user);
    ValidateUserFields(user.UserName, user.Email);
    if (string.IsNullOrEmpty(password)) throw new ArgumentException("Укажите пароль.");
    var created = new ApplicationUser { UserName = user.UserName!.Trim(), Email = user.Email!.Trim(), PersonalId = user.PersonalId };
    var result = await WriteAsync(async (db,manager)=>{
      await CheckPersonalAsync(db,created.PersonalId,ct);
      if (created.PersonalId.HasValue && await db.Users.AnyAsync(u => u.PersonalId == created.PersonalId, ct))
        throw new InvalidOperationException("У выбранного сотрудника уже есть учётная запись.");
      Check(await manager.CreateAsync(created,password));
    },ct,eventType: AuditEventType.UserCreated, createdId: () => created.Id);
    user.Id = created.Id;
    return result;
  }
  public async Task SetUserLockoutAsync(string userId, bool lockout, CancellationToken ct = default) =>
    Check(await ToggleUserLockoutAsync(userId, lockout, ct));

  private Task<IdentityResult> ToggleUserLockoutAsync(string userId,bool lockout,CancellationToken ct=default) =>
    WriteAsync(async(db,manager)=>{
      var user=await manager.FindByIdAsync(userId)??throw new KeyNotFoundException("Пользователь не найден.");
      await CheckTargetAsync(db,user,ct);
      if(lockout && await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId==userId && r.NormalizedName=="ROOT" select ur).AnyAsync(ct))
        throw new InvalidOperationException("Системную учётную запись Root нельзя блокировать.");
      user.AccountBlockReason = lockout ? "Administrator" : null;
      if (!lockout) user.UnblockedAtUtc = DateTimeOffset.UtcNow;
      Check(await manager.SetLockoutEnabledAsync(user,true));
      Check(await manager.SetLockoutEndDateAsync(user,lockout?DateTimeOffset.MaxValue:null));
      if (!lockout) Check(await manager.ResetAccessFailedCountAsync(user));
      Check(await manager.UpdateSecurityStampAsync(user));
    },ct,userId,lockout ? AuditEventType.UserBlocked : AuditEventType.UserUnblocked);
  private Task<IdentityResult> ResetPasswordAsync(string userId,string newPassword,CancellationToken ct=default) =>
    WriteAsync(async(db,manager)=>{
      var user=await manager.FindByIdAsync(userId)??throw new KeyNotFoundException("Пользователь не найден.");
      await CheckTargetAsync(db,user,ct);
      if (user.AccountBlockReason != null || await manager.IsLockedOutAsync(user))
        throw new UserSecurityException("Сначала разблокируйте пользователя.");
      Check(await manager.ResetPasswordAsync(user,await manager.GeneratePasswordResetTokenAsync(user),newPassword));
      user.MustChangePassword = true;
      Check(await manager.UpdateAsync(user));
    },ct,userId,AuditEventType.PasswordChanged);
}
