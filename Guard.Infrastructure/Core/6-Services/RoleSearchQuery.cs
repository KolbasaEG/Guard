using System.Collections.Frozen;
using Guard.Core.Entities;
using Guard.Core.Services.Exports;

namespace Guard.Core.Services;

public static class RoleSearchQuery
{
  public static readonly FrozenSet<string> Fields = RoleSearchFilter.Fields;
  public static void ValidateFilter(ExportFilter? filter) => RoleSearchFilter.Validate(filter);
  public static IQueryable<ApplicationRole> Filter(IQueryable<ApplicationRole> query, ExportFilter? filter) => RestrictedSearchQuery<ApplicationRole>.Filter(query, filter, Fields);
  public static IOrderedQueryable<ApplicationRole> Sort(IQueryable<ApplicationRole> query, string? orderBy) => RestrictedSearchQuery<ApplicationRole>.Sort(query, orderBy, Fields, "Name asc", "Id");
}
