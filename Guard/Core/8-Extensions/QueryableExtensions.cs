using Guard.Core.Entities;
using Guard.Core.Enums;
using Radzen;

namespace Guard.Core.Extensions;

public static class QueryableExtensions
{
  //метод расширения для IQueryable<T> позволяющий фильтровать по DataViewMode (активные, архивные, удаленные)
  public static IQueryable<T> FilterByMode<T>(this IQueryable<T> query, DataViewMode mode) where T : BaseEntity
  {
    return mode switch
    {
      DataViewMode.Active => query.Where(e => e.Status == Status.Inserted
                                           || e.Status == Status.Modified
                                           || e.Status == Status.Blocked),

      DataViewMode.Archived => query.Where(e => e.Status == Status.Archived
                                             || e.Status == Status.ArchivedBlocked),

      DataViewMode.Deleted => query.Where(e => e.Status == Status.Deleted),

      _ => query
    };
  }

}