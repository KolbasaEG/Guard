using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;
public class CurrentUserService(IPermissionService permissions, IDataAccessScopeService scopes,
    IDbContextFactory<ApplicationDbContext> factory, IHttpContextAccessor http) : ICurrentUserService
{
  private UserContext? cached;
  public string? UserId => cached?.UserId ?? http.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
  public string? UserName => cached?.UserName ?? http.HttpContext?.User.Identity?.Name;
  public void RefreshContext() => cached = null;
  public async Task<UserContext?> GetContextAsync(CancellationToken ct = default)
  {
    // Модель отображения; серверные разрешения всегда вычисляются отдельно.
    Guard.Core.Services.DTOs.UserAccessSnapshot access;
    try { access = await permissions.GetCurrentAsync(ct); }
    catch (UnauthorizedAccessException) { cached = null; return null; }
    var scope = await scopes.GetAsync(ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    var user = await db.Users.AsNoTracking().Include(u => u.Personal).ThenInclude(p => p!.Subdivision)
        .SingleOrDefaultAsync(u => u.Id == access.UserId, ct);
    if (user == null) return null;
    var subdivisions = await db.Subdivisions.AsNoTracking().Where(s =>
        (scope.IsRoot || scope.SubdivisionIds.Contains(s.Id)) && (s.Status == Status.Inserted || s.Status == Status.Modified))
        .OrderBy(s => s.Name).ToListAsync(ct);
    cached = new UserContext { UserId = user.Id, UserName = user.UserName ?? "", IsRoot = access.IsRoot,
      User = user, Personal = user.Personal, Subdivision = user.Personal?.Subdivision,
      SubordinateSubdivisions = subdivisions, AccessibleSubdivisionIds = scope.SubdivisionIds };
    return cached;
  }
}
