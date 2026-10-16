using System.Collections.Frozen;
using Guard.Core.Entities;
using Guard.Core.Services.Exports;

namespace Guard.Core.Services;

public static class UserSearchQuery
{
  public static readonly FrozenSet<string> Fields = new[] { "Id", "UserName", "Email", "PersonalId", "AccountBlockReason", "LastActivityAtUtc", "MustChangePassword" }.ToFrozenSet(StringComparer.Ordinal);
  public static void ValidateFilter(ExportFilter? filter) => RestrictedSearchQuery<ApplicationUser>.ValidateFilter(filter, Fields);
  public static IQueryable<ApplicationUser> Filter(IQueryable<ApplicationUser> query, ExportFilter? filter) => RestrictedSearchQuery<ApplicationUser>.Filter(query, filter, Fields);
  public static IOrderedQueryable<ApplicationUser> Sort(IQueryable<ApplicationUser> query, string? orderBy) => RestrictedSearchQuery<ApplicationUser>.Sort(query, orderBy, Fields, "Id asc", "Id");
}
