using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services.DTOs;
using Guard.Core.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Guard.Core.Services;

// Менеджеры Identity разрешаются в отдельном scope: один контекст и транзакция на операцию.
public class RoleAccessService(IPermissionService permissions, IServiceScopeFactory scopes, IAuditService audit, IAccessChangeNotifier notifier)
    : IRoleAccessService
{
  private async Task<string> RequireRootAsync(CancellationToken ct)
  {
    var current = await permissions.GetCurrentAsync(ct);
    if (!current.IsRoot) throw new UnauthorizedAccessException("Управление ролями доступно только Root.");
    return current.UserId;
  }
  private static void Check(IdentityResult result)
  {
    if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
  }
  private static string UserVersion(string? stamp, IEnumerable<string> ids) =>
      (stamp ?? "") + ":" + string.Join(",", ids.OrderBy(id => id, StringComparer.Ordinal));

  public async Task<RoleEditDto> GetAsync(string id, CancellationToken ct = default)
  {
    await RequireRootAsync(ct);
    await using var scope = scopes.CreateAsyncScope();
    var manager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    var role = await manager.FindByIdAsync(id) ?? throw new KeyNotFoundException("Роль не найдена.");
    var claims = await manager.GetClaimsAsync(role);
    return new() { Id = role.Id, Name = role.Name ?? "", Version = role.ConcurrencyStamp,
      IsSystem = role.NormalizedName == "ROOT", Permissions = claims.Where(c => c.Type == "Permission").Select(c => c.Value).ToHashSet() };
  }

  public async Task<string> SaveAsync(RoleEditDto input, CancellationToken ct = default)
  {
    var actor = await RequireRootAsync(ct);
    if (string.IsNullOrWhiteSpace(input.Name)) throw new ArgumentException("Введите название роли.");
    var selected = PermissionCatalog.Validate(input.Permissions);
    await using var scope = scopes.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var manager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)", ct);
    actor = await RequireRootAsync(ct);
    var create = string.IsNullOrEmpty(input.Id);
    var role = create ? new ApplicationRole() :
      await manager.FindByIdAsync(input.Id) ?? throw new KeyNotFoundException("Роль не найдена.");
    if (!create && role.ConcurrencyStamp != input.Version)
      throw new InvalidOperationException("Роль уже изменена другим оператором. Откройте её заново.");
    if (role.NormalizedName == "ROOT" || input.Name.Trim().Equals("Root", StringComparison.OrdinalIgnoreCase))
      throw new InvalidOperationException("Root — защищённая системная роль.");
    var old = create ? new List<Claim>() : (await manager.GetClaimsAsync(role)).Where(c => c.Type == "Permission").ToList();
    role.Name = input.Name.Trim();
    if (create) Check(await manager.CreateAsync(role)); else Check(await manager.UpdateAsync(role));
    foreach (var claim in old.Where(c => !selected.Contains(c.Value))) Check(await manager.RemoveClaimAsync(role, claim));
    foreach (var code in selected.Except(old.Select(c => c.Value))) Check(await manager.AddClaimAsync(role, new Claim("Permission", code)));
    var affectedUsers = await db.UserRoles.Where(r => r.RoleId == role.Id).Select(r => r.UserId).ToListAsync(ct);
    // Обновление stamp фиксирует также изменения только набора claims.
    Check(await manager.UpdateAsync(role));
    await tx.CommitAsync(ct);
    await notifier.PublishAsync(affectedUsers);
    audit.LogIdentityEvent(AuditEventType.RolePermissionsChanged, actor, details:
        $"RoleId={role.Id}; added={string.Join(",", selected.Except(old.Select(c => c.Value)))}; removed={string.Join(",", old.Select(c => c.Value).Except(selected))}");
    return role.Id;
  }

  public async Task DeleteAsync(string id, CancellationToken ct = default)
  {
    var actor = await RequireRootAsync(ct);
    await using var scope = scopes.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var manager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)", ct);
    actor = await RequireRootAsync(ct);
    var role = await manager.FindByIdAsync(id) ?? throw new KeyNotFoundException("Роль не найдена.");
    if (role.NormalizedName == "ROOT") throw new InvalidOperationException("Нельзя удалить Root.");
    var affectedUsers = await db.UserRoles.Where(r => r.RoleId == id).Select(r => r.UserId).ToListAsync(ct);
    Check(await manager.DeleteAsync(role));
    await tx.CommitAsync(ct);
    await notifier.PublishAsync(affectedUsers);
    audit.LogIdentityEvent(AuditEventType.RolePermissionsChanged, actor, details: $"Deleted role {id}");
  }

  public async Task<UserRoleAssignmentsDto> GetUserRolesAsync(string userId, CancellationToken ct = default)
  {
    await RequireRootAsync(ct);
    await using var scope = scopes.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct)
        ?? throw new KeyNotFoundException("Пользователь не найден.");
    var roles = await db.Roles.OrderBy(r => r.Name).Select(r => new RoleOptionDto(r.Id, r.Name!)).ToListAsync(ct);
    var selected = await db.UserRoles.Where(r => r.UserId == userId).Select(r => r.RoleId).ToListAsync(ct);
    return new(roles, selected, UserVersion(user.ConcurrencyStamp, selected));
  }

  public async Task SetUserRolesAsync(string userId, IEnumerable<string> roles, string version, CancellationToken ct = default)
  {
    var actor = await RequireRootAsync(ct);
    var selected = roles.Distinct().ToArray();
    await using var scope = scopes.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)", ct);
    actor = await RequireRootAsync(ct);
    var user = await users.FindByIdAsync(userId) ?? throw new KeyNotFoundException("Пользователь не найден.");
    var current = await db.UserRoles.Where(r => r.UserId == userId).Select(r => r.RoleId).ToListAsync(ct);
    if (UserVersion(user.ConcurrencyStamp, current) != version)
      throw new InvalidOperationException("Назначения уже изменены. Откройте форму заново.");
    var found = await db.Roles.Where(r => selected.Contains(r.Id)).ToListAsync(ct);
    if (found.Count != selected.Length) throw new ArgumentException("Выбрана неизвестная роль.");
    var rootId = await db.Roles.Where(r => r.NormalizedName == "ROOT").Select(r => r.Id).SingleOrDefaultAsync(ct);
    if (rootId != null && current.Contains(rootId) && !selected.Contains(rootId) &&
        !await (from ur in db.UserRoles join u in db.Users on ur.UserId equals u.Id
          where ur.RoleId == rootId && ur.UserId != userId && (u.LockoutEnd == null || u.LockoutEnd <= DateTimeOffset.UtcNow)
          select ur).AnyAsync(ct))
      throw new InvalidOperationException("Нельзя снять роль у последнего Root.");
    var removeNames = await db.Roles.Where(r => current.Contains(r.Id) && !selected.Contains(r.Id)).Select(r => r.Name!).ToListAsync(ct);
    var addNames = found.Where(r => !current.Contains(r.Id)).Select(r => r.Name!).ToArray();
    Check(await users.RemoveFromRolesAsync(user, removeNames));
    Check(await users.AddToRolesAsync(user, addNames));
    await tx.CommitAsync(ct);
    await notifier.PublishAsync([userId]);
    audit.LogIdentityEvent(AuditEventType.UserRolesChanged, actor, details: $"UserId={userId}; added={string.Join(",", addNames)}; removed={string.Join(",", removeNames)}");
  }

  public async Task<PermissionTransitionReport> GetTransitionReportAsync(CancellationToken ct = default)
  {
    await RequireRootAsync(ct);
    await using var scope = scopes.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var count = await db.UserClaims.CountAsync(c => c.ClaimType == "Permission", ct);
    var roles = await db.Roles.Where(r => r.NormalizedName != "ROOT" &&
        !db.RoleClaims.Any(c => c.RoleId == r.Id && c.ClaimType == "Permission")).Select(r => r.Name!).ToListAsync(ct);
    return new(count, roles);
  }

  public async Task InitializeAdministratorAsync(CancellationToken ct = default)
  {
    var actor = await RequireRootAsync(ct);
    await using var scope = scopes.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var manager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162002)", ct);
    actor = await RequireRootAsync(ct);
    var role = await manager.FindByNameAsync("Администратор") ?? throw new KeyNotFoundException("Роль Администратор не найдена.");
    if ((await manager.GetClaimsAsync(role)).Any(c => c.Type == "Permission"))
      throw new InvalidOperationException("Роль уже настроена. Начальные права не перезаписывают существующие.");
    var affectedUsers = await db.UserRoles.Where(r => r.RoleId == role.Id).Select(r => r.UserId).ToListAsync(ct);
    foreach (var p in PermissionCatalog.All.Where(p => !p.RootOnly))
      Check(await manager.AddClaimAsync(role, new Claim("Permission", p.Code)));
    Check(await manager.UpdateAsync(role));
    await tx.CommitAsync(ct);
    await notifier.PublishAsync(affectedUsers);
    audit.LogIdentityEvent(AuditEventType.RolePermissionsChanged, actor, details: "Initialized Administrator permissions");
  }
}
