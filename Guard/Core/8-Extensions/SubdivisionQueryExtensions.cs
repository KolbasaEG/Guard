using Guard.Core.Entities;
using Guard.Core.Enums;

namespace Guard.Core.Extensions;

public static class SubdivisionQueryExtensions
{
  public static IQueryable<T> FilterBySubdivision<T>(
      this IQueryable<T> query,
      string? targetPath,
      SubdivisionHierarchyMode mode = SubdivisionHierarchyMode.IncludeChildren)
      where T : class, IHasSubdivision
  {
    if (string.IsNullOrWhiteSpace(targetPath))
      return query;

    return mode switch
    {
      SubdivisionHierarchyMode.CurrentOnly => query.Where(e =>
          e.Subdivision != null && e.Subdivision.Path == targetPath),

      SubdivisionHierarchyMode.IncludeChildren => query.Where(e =>
          e.Subdivision != null && e.Subdivision.Path.StartsWith(targetPath)),

      _ => query
    };
  }
}