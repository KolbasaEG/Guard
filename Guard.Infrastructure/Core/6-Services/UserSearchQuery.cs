using System.Collections.Frozen;
using Guard.Core.Entities;
using Guard.Core.Services.Exports;

namespace Guard.Core.Services;

public static class UserSearchQuery
{
  public static readonly FrozenSet<string> Fields = UserSearchFilter.Fields;
  public static void ValidateFilter(ExportFilter? filter) => UserSearchFilter.Validate(filter);
  public static IQueryable<ApplicationUser> Filter(IQueryable<ApplicationUser> query, ExportFilter? filter) => RestrictedSearchQuery<ApplicationUser>.Filter(query, filter, Fields);
  public static IOrderedQueryable<ApplicationUser> Sort(IQueryable<ApplicationUser> query, string? orderBy) => RestrictedSearchQuery<ApplicationUser>.Sort(query, orderBy, Fields, "Id asc", "Id");
}
