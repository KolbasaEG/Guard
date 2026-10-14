using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services.Exports;

public sealed record ExportAccessScope(UserAccessSnapshot User, Guid? OwnId, string? OwnPath, HashSet<Guid> AllowedIds, string Stamp);

public sealed class ExportAccess(IDbContextFactory<ApplicationDbContext> factory, IPermissionService permissions)
{
  public static bool ValidPath(string? path) => path != null && path.StartsWith('/') && path.EndsWith('/') &&
    path.Split('/', StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } parts && parts.All(p => long.TryParse(p, out var n) && n > 0);
  public async Task<ExportAccessScope> ForUserAsync(string id, ExportRequest request, CancellationToken ct)
  {
    var user = await permissions.GetForUserAsync(id, ct);
    if (!user.Has(ExportSchema.Permission(request.Area))) throw new UnauthorizedAccessException("Недостаточно прав для экспорта.");
    if (request.AllSubdivisions && !user.IsRoot) throw new UnauthorizedAccessException("Все подразделения доступны только Root.");
    if (request.Mode == DataViewMode.Deleted && !user.IsRoot) throw new UnauthorizedAccessException("Удалённые записи доступны только Root.");
    var archivePermission = request.Area == ExportArea.Personals ? Permissions.Personals.ReadArchive : Permissions.Subdivisions.ReadArchive;
    if (request.Mode == DataViewMode.Archived && request.Area != ExportArea.IpAddresses && !user.Has(archivePermission))
      throw new UnauthorizedAccessException("Недостаточно прав для архивных записей.");
    await using var db = await factory.CreateDbContextAsync(ct);
    var own = await db.Users.Where(u => u.Id == id).Select(u => new {
      u.SecurityStamp, OwnId = u.Personal == null ? null : u.Personal.SubdivisionId,
      Path = u.Personal == null || u.Personal.Subdivision == null ? null : u.Personal.Subdivision.Path
    }).SingleOrDefaultAsync(ct) ?? throw new UnauthorizedAccessException("Учётная запись недоступна.");
    var ids = !ValidPath(own.Path) ? [] : await db.Subdivisions.Where(s => s.Path.StartsWith(own.Path!)).Select(s => s.Id).ToListAsync(ct);
    return new(user, own.OwnId, own.Path, ids.ToHashSet(), own.SecurityStamp ?? "");
  }
  public static void CheckDelivery(ExportAccessScope scope, ExportJob job, IEnumerable<Guid> subdivisions, bool unassigned)
  {
    if (scope.Stamp != job.SecurityStamp) throw new UnauthorizedAccessException("Сессия автора изменилась. Создайте новую выгрузку.");
    if (!scope.User.IsRoot && (unassigned || subdivisions.Any(id => !scope.AllowedIds.Contains(id))))
      throw new UnauthorizedAccessException("Область доступа изменилась. Создайте новую выгрузку.");
  }
}
