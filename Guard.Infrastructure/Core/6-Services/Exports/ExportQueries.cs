using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;

namespace Guard.Core.Services.Exports;

public static class ExportQueries
{
  public static readonly HashSet<string> SubdivisionFields = ["Address", "Fax", "Id", "InsertedDate", "IsDepartment", "LevelOrder", "Name", "OrganType.Value", "Parent.Name", "Path", "Phone", "PositionFormationName", "PostalCode", "StaffCount", "StatusClassifier.Value"];
  public static readonly HashSet<string> IpFields = ["Subdivision.Name", "Address", "Description"];
  public static IQueryable<T> Statuses<T>(IQueryable<T> query, DataViewMode mode) where T : BaseEntity => mode switch {
    DataViewMode.Active => query.Where(p => p.Status == Status.Inserted || p.Status == Status.Modified || p.Status == Status.Blocked),
    DataViewMode.Archived => query.Where(p => p.Status == Status.Archived || p.Status == Status.ArchivedBlocked),
    DataViewMode.Deleted => query.Where(p => p.Status == Status.Deleted),
    _ => throw new ArgumentException("Неизвестный режим просмотра.")
  };
  public static IQueryable<T> Sort<T>(IQueryable<T> query, string? order, IReadOnlySet<string> fields)
  {
    if (string.IsNullOrWhiteSpace(order)) return query.OrderBy("InsertedDate desc, Id asc");
    var clauses = new List<string>();
    foreach (var clause in order.Split(',')) {
      var parts = clause.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length is < 1 or > 2 || !fields.Contains(parts[0])) throw new ArgumentException("Недопустимое поле сортировки.");
      var direction = parts.Length == 1 ? "asc" : parts[1].ToLowerInvariant();
      if (direction is not ("asc" or "desc")) throw new ArgumentException("Недопустимое направление сортировки.");
      clauses.Add(parts[0] + " " + direction);
    }
    clauses.Add("Id asc"); return query.OrderBy(string.Join(", ", clauses));
  }
  public static async Task<IQueryable<Personal>> PersonalsAsync(ApplicationDbContext db, PersonalSearchRequest request,
    UserAccessSnapshot user, DataAccessScope scope, CancellationToken ct)
  {
    if (!user.Has(Permissions.Personals.Read)) throw new UnauthorizedAccessException("Недостаточно прав для чтения сотрудников.");
    var q = db.Personals.AsNoTracking().AsQueryable();
    if (request.AllSubdivisions) {
      if (!scope.IsRoot) throw new UnauthorizedAccessException("Все подразделения доступны только Root.");
      request = request with { OwnSubdivision = false, SubdivisionId = null };
    }
    if (!scope.IsRoot) q = q.Where(p => p.SubdivisionId.HasValue && scope.SubdivisionIds.Contains(p.SubdivisionId.Value));
    if (request.OwnSubdivision) {
      var ownId = await db.Users.Where(u => u.Id == scope.UserId).Select(u => u.Personal == null ? null : u.Personal.SubdivisionId).SingleOrDefaultAsync(ct);
      if (!ownId.HasValue) q = q.Where(p => false);
      request = request with { SubdivisionId = ownId };
    }
    if (request.Mode == DataViewMode.Archived && !user.Has(Permissions.Personals.ReadArchive) || request.Mode == DataViewMode.Deleted && !scope.IsRoot)
      throw new UnauthorizedAccessException("Недостаточно прав для выбранного режима.");
    q = Statuses(q, request.Mode);
    if (request.SubdivisionId is Guid id) {
      if (!scope.Allows(id)) throw new UnauthorizedAccessException("Подразделение недоступно.");
      var path = await db.Subdivisions.Where(s => s.Id == id).Select(s => s.Path).SingleOrDefaultAsync(ct);
      if (!ExportAccess.ValidPath(path)) return q.Where(p => false);
      q = request.IncludeChildren ? q.Where(p => p.Subdivision != null && p.Subdivision.Path.StartsWith(path!)) : q.Where(p => p.SubdivisionId == id);
    }
    if (!string.IsNullOrWhiteSpace(request.Search)) {
      var term = request.Search.Trim().ToLower();
      q = q.Where(p => p.LastName.ToLower().Contains(term) || p.FirstName.ToLower().Contains(term) || p.MiddleName != null && p.MiddleName.ToLower().Contains(term));
    }
    return PersonalListQuery.Sort(PersonalQueryFilter.Apply(q, request.Filter), request.OrderBy);
  }
  public static IQueryable<Subdivision> Subdivisions(ApplicationDbContext db, ExportRequest request, ExportAccessScope scope)
  {
    var q = db.Subdivisions.AsNoTracking().AsQueryable();
    if (!scope.User.IsRoot) q = q.Where(s => scope.AllowedIds.Contains(s.Id));
    return SubdivisionSearchQuery.Sort(SubdivisionSearchQuery.Filter(SubdivisionSearchQuery.Visibility(q, request.Mode,
      request.AllSubdivisions, scope.User.IsRoot, scope.OwnPath,
      request.IncludeChildren ? SubdivisionHierarchyMode.IncludeChildren : SubdivisionHierarchyMode.CurrentOnly), request.Filter), request.OrderBy);
  }
  public static IQueryable<IpAddress> IpAddresses(ApplicationDbContext db, ExportRequest request, ExportAccessScope scope)
  {
    if (request.AllSubdivisions && !scope.User.IsRoot) throw new UnauthorizedAccessException("Все IP-адреса доступны только Root.");
    var q = db.IpAddresses.AsNoTracking().AsQueryable();
    if (!scope.User.IsRoot) q = q.Where(p => p.SubdivisionId.HasValue && scope.AllowedIds.Contains(p.SubdivisionId.Value));
    if (!request.AllSubdivisions) q = !ExportAccess.ValidPath(scope.OwnPath) ? q.Where(p => false) :
      request.IncludeChildren ? q.Where(p => p.Subdivision != null && p.Subdivision.Path.StartsWith(scope.OwnPath!)) : q.Where(p => p.Subdivision != null && p.Subdivision.Path == scope.OwnPath);
    return IpSearchQuery.Sort(IpSearchQuery.Filter(Statuses(q, request.Mode), request.Filter), request.OrderBy);
  }
}
