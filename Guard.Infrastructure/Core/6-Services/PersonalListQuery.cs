using Guard.Core.Entities;
using System.Linq.Expressions;

namespace Guard.Core.Services;

public static class PersonalListQuery
{
  public static IOrderedQueryable<Personal> Sort(IQueryable<Personal> query, string? orderBy)
  {
    if (string.IsNullOrWhiteSpace(orderBy))
      return query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ThenBy(p => p.Id);
    var parts = orderBy.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length > 2 || parts.Length == 2 && parts[1] is not ("asc" or "desc"))
      throw new ArgumentException("Недопустимые параметры сортировки сотрудников.");
    Expression<Func<Personal, string?>> field = parts[0] switch
    {
      "LastName" => p => p.LastName,
      "FirstName" => p => p.FirstName,
      "MiddleName" => p => p.MiddleName,
      "FullName" => p => p.FullName,
      "Subdivision.Name" or "SubdivisionName" => p => p.Subdivision == null ? null : p.Subdivision.Name,
      _ => throw new ArgumentException("Недопустимое поле сортировки сотрудников.")
    };
    var sorted = parts.Length == 2 && parts[1] == "desc" ? query.OrderByDescending(field) : query.OrderBy(field);
    if (parts[0] == "LastName") sorted = sorted.ThenBy(p => p.FirstName);
    return sorted.ThenBy(p => p.Id);
  }
}
