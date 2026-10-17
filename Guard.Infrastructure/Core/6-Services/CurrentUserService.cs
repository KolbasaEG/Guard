using Guard.Core.Contexts;
using Guard.Core.Enums;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;
public class CurrentUserService(IPermissionService permissions, IDataAccessScopeService scopes,
    IDbContextFactory<ApplicationDbContext> factory) : ICurrentUserDisplayService
{
  public async Task<CurrentUserDisplayDto?> GetDisplayAsync(CancellationToken ct = default)
  {
    UserAccessSnapshot access;
    try { access = await permissions.GetCurrentAsync(ct); }
    catch (UnauthorizedAccessException) { return null; }
    var scope = await scopes.GetAsync(ct);
    if (scope.UserId != access.UserId || scope.IsRoot != access.IsRoot)
      throw new UnauthorizedAccessException("Контекст доступа изменился. Обновите страницу.");
    await using var db = await factory.CreateDbContextAsync(ct);
    var user = await db.Users.AsNoTracking().Where(u => u.Id == access.UserId)
        .Select(u => new { u.Id, u.UserName,
          PersonalFullName = u.Personal == null ? null : u.Personal.FullName,
          SubdivisionName = u.Personal == null || u.Personal.Subdivision == null ? null : u.Personal.Subdivision.Name })
        .SingleOrDefaultAsync(ct);
    if (user == null) return null;
    var subdivisions = await db.Subdivisions.AsNoTracking().Where(s =>
        (scope.IsRoot || scope.SubdivisionIds.Contains(s.Id)) && (s.Status == Status.Inserted || s.Status == Status.Modified))
        .OrderBy(s => s.Name).ThenBy(s => s.Id)
        .Select(s => new CurrentUserSubdivisionDto(s.Id, s.Name)).ToListAsync(ct);
    var current = await permissions.GetCurrentAsync(ct);
    var currentScope = await scopes.GetAsync(ct);
    ct.ThrowIfCancellationRequested();
    if (current.UserId != access.UserId || current.IsRoot != access.IsRoot ||
        !current.Permissions.SetEquals(access.Permissions) || currentScope.UserId != scope.UserId ||
        currentScope.IsRoot != scope.IsRoot || !currentScope.SubdivisionIds.SetEquals(scope.SubdivisionIds))
      throw new UnauthorizedAccessException("Контекст доступа изменился. Обновите страницу.");
    return new(user.Id, user.UserName ?? "", access.IsRoot, user.PersonalFullName,
        user.SubdivisionName, subdivisions.AsReadOnly());
  }
}
