
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace Guard.Core.Services;
public class SecurityService(
  IReadRepository<ApplicationUser> userReads, IReadRepository<ApplicationRole> roleReads, IReadRepository<Personal> personalReads,
  IIpAddressService ips, IPersonalIpService assignments, IIpAccessService ipGate, IPermissionService permissions,
  IDataAccessScopeService accessScopes, IRoleAccessService roles, IServiceScopeFactory scopes,
  IAuditService audit, IAccessChangeNotifier notifier) : ISecurityService
{
  public Task<TResult> QueryUsersAsync<TResult>(Func<IQueryable<ApplicationUser>,Task<TResult>> query, CancellationToken ct=default) => userReads.QueryAsync(query,ct);
  public Task<TResult> QueryRolesAsync<TResult>(Func<IQueryable<ApplicationRole>,Task<TResult>> query, CancellationToken ct=default) => roleReads.QueryAsync(query,ct);
  public Task<TResult> QueryPersonalsAsync<TResult>(Func<IQueryable<Personal>,Task<TResult>> query, CancellationToken ct=default) => personalReads.QueryAsync(query,ct);
  public Task<TResult> QueryIpAddressesAsync<TResult>(Func<IQueryable<IpAddress>,Task<TResult>> query, CancellationToken ct=default) => ips.QueryIpAddressesAsync(query,ct);
  public Task<ApplicationUser?> GetUserByIdAsync(string id, CancellationToken ct=default) => userReads.GetByIdAsync(id,ct);
  public async Task<IReadOnlyList<Guard.Core.Services.DTOs.UserPersonalOption>> GetUserPersonalOptionsAsync(CancellationToken ct=default)
  {
    await permissions.RequireAsync(Permissions.Users.Manage, ct);
    var access = await accessScopes.GetAsync(ct);
    await using var scope = scopes.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var query = db.Set<Personal>().AsNoTracking().Where(p =>
      (p.Status == Status.Inserted || p.Status == Status.Modified) && !db.Users.Any(u => u.PersonalId == p.Id));
    if (!access.IsRoot) query = query.Where(p => p.SubdivisionId.HasValue && access.SubdivisionIds.Contains(p.SubdivisionId.Value));
    return await query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
      .Select(p => new Guard.Core.Services.DTOs.UserPersonalOption(p.Id,
        p.LastName + " " + p.FirstName + " " + (p.MiddleName ?? ""))).ToListAsync(ct);
  }
  private static void Check(IdentityResult result) {
    if (!result.Succeeded) throw new UserSecurityException(string.Join("; ", result.Errors.Select(e=>e.Description)));
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
    string? targetId = null, AuditEventType? eventType = null)
  {
    await permissions.RequireAsync(Permissions.Users.Manage,ct);
    var actorId = (await permissions.GetCurrentAsync(ct)).UserId;
    await using var scope = scopes.CreateAsyncScope();
    var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await using var tx=await db.Database.BeginTransactionAsync(ct);
    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)",ct);
    await permissions.RequireAsync(Permissions.Users.Manage,ct);
    await write(db,manager);
    await tx.CommitAsync(ct);
    if (targetId != null) {
      if (eventType.HasValue) audit.LogIdentityEvent(eventType.Value, actorId, details: $"Target={targetId}");
      await notifier.PublishAsync([targetId]);
    }
    return IdentityResult.Success;
  }
  public Task<IdentityResult> CreateUserAsync(ApplicationUser user,string password,IEnumerable<string>? roles=null,CancellationToken ct=default) =>
    WriteAsync(async (db,manager)=>{
      await CheckPersonalAsync(db,user.PersonalId,ct);
      if (user.PersonalId.HasValue && await db.Users.AnyAsync(u => u.PersonalId == user.PersonalId, ct))
        throw new InvalidOperationException("У выбранного сотрудника уже есть учётная запись.");
      var selected=(roles??[]).Distinct().ToArray();
      if(selected.Length>0 && !(await permissions.GetCurrentAsync(ct)).IsRoot) throw new UnauthorizedAccessException("Роли назначает только Root.");
      // Входные флаги безопасности не позволяют создавать привилегированную учётную запись.
      var created=new ApplicationUser { UserName=user.UserName, Email=user.Email, PersonalId=user.PersonalId };
      Check(await manager.CreateAsync(created,password));
      if(selected.Length>0) Check(await manager.AddToRolesAsync(created,selected));
      user.Id=created.Id;
    },ct);
  public Task<IdentityResult> UpdateUserAsync(ApplicationUser user,IEnumerable<string> roles,CancellationToken ct=default) =>
    WriteAsync(async(db,manager)=>{
      var current=await manager.FindByIdAsync(user.Id)??throw new KeyNotFoundException("Пользователь не найден.");
      await CheckTargetAsync(db,current,ct);
      await CheckPersonalAsync(db,user.PersonalId,ct);
      var old=await manager.GetRolesAsync(current);
      var selected=roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
      if(!old.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(selected))
        throw new InvalidOperationException("Назначайте роли в отдельной форме с проверкой версии.");
      current.Email=user.Email; current.UserName=user.UserName; current.PersonalId=user.PersonalId;
      Check(await manager.UpdateAsync(current));
    },ct);
  public Task<IdentityResult> ToggleUserLockoutAsync(string userId,bool lockout,CancellationToken ct=default) =>
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
  public Task<IdentityResult> ResetPasswordAsync(string userId,string newPassword,CancellationToken ct=default) =>
    WriteAsync(async(db,manager)=>{
      var user=await manager.FindByIdAsync(userId)??throw new KeyNotFoundException("Пользователь не найден.");
      await CheckTargetAsync(db,user,ct);
      if (user.AccountBlockReason != null || await manager.IsLockedOutAsync(user))
        throw new UserSecurityException("Сначала разблокируйте пользователя.");
      Check(await manager.ResetPasswordAsync(user,await manager.GeneratePasswordResetTokenAsync(user),newPassword));
      user.MustChangePassword = true;
      Check(await manager.UpdateAsync(user));
    },ct,userId,AuditEventType.PasswordChanged);
  public async Task<List<string>> GetUserRolesAsync(string userId,CancellationToken ct=default) {
    if(await userReads.GetByIdAsync(userId,ct)==null) throw new KeyNotFoundException("Пользователь недоступен.");
    await using var scope=scopes.CreateAsyncScope();
    var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    return (await manager.GetRolesAsync((await manager.FindByIdAsync(userId))!)).ToList();
  }
  public async Task<List<string>> GetRoleClaimsAsync(string roleId,CancellationToken ct=default) => (await roles.GetAsync(roleId,ct)).Permissions.ToList();
  public async Task<IdentityResult> UpdateRoleClaimsAsync(string roleId,IEnumerable<string> values,CancellationToken ct=default) {
    var role=await roles.GetAsync(roleId,ct); role.Permissions=values.ToHashSet(StringComparer.Ordinal);
    await roles.SaveAsync(role,ct); return IdentityResult.Success;
  }
  public async Task<List<string>> GetUserClaimsAsync(string userId,CancellationToken ct=default) {
    await permissions.RequireAsync(Permissions.Roles.Read,ct);
    await using var scope=scopes.CreateAsyncScope();
    var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    return await db.UserClaims.Where(c=>c.UserId==userId && c.ClaimType=="Permission").Select(c=>c.ClaimValue!).ToListAsync(ct);
  }
  public Task<IdentityResult> UpdateUserClaimsAsync(string userId,IEnumerable<string> values,CancellationToken ct=default) =>
    throw new NotSupportedException("Индивидуальные permissions сохранены для истории. Настраивайте права через роли.");
  public Task UpdatePersonalIpAddressesAsync(Guid personalId,IEnumerable<Guid> ids,CancellationToken ct=default) => assignments.UpdateAsync(personalId,ids,ct);
  public Task<bool> IsIpAllowedForUserAsync(string userId,string clientIp,CancellationToken ct=default) => ipGate.IsAllowedAsync(userId,clientIp,ct);
}
