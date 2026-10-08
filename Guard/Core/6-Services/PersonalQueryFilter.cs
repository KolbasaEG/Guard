using Guard.Core.Entities;
using Guard.Core.Services.DTOs;
using System.Linq.Expressions;

namespace Guard.Core.Services;

public static class PersonalQueryFilter
{
  public static IQueryable<Personal> Apply(IQueryable<Personal> query, PersonalFilter? filter)
  {
    if (filter == null) return query;
    var parameter = Expression.Parameter(typeof(Personal), "p"); var count = 0;
    return query.Where(Expression.Lambda<Func<Personal, bool>>(Build(filter, parameter, 0, ref count), parameter));
  }
  private static Expression Build(PersonalFilter filter, ParameterExpression parameter, int depth, ref int count)
  {
    if (depth > 16 || ++count > 100) throw new ArgumentException("Слишком сложный фильтр.");
    if (filter.Children != null) {
      Expression? group = null;
      foreach (var child in filter.Children) {
        var item = Build(child, parameter, depth + 1, ref count);
        group = group == null ? item : filter.All ? Expression.AndAlso(group, item) : Expression.OrElse(group, item);
      }
      return group ?? Expression.Constant(true);
    }
    if (filter.Field is not ("LastName" or "FirstName" or "MiddleName" or "FullName" or "Subdivision.Name"))
      throw new ArgumentException("Недопустимое поле фильтра сотрудников.");
    Expression field; Expression? parentExists = null;
    if (filter.Field == "Subdivision.Name") {
      var subdivision = Expression.Property(parameter, nameof(Personal.Subdivision));
      parentExists = Expression.NotEqual(subdivision, Expression.Constant(null, typeof(Subdivision)));
      field = Expression.Property(subdivision, nameof(Subdivision.Name));
    }
    else field = Expression.Property(parameter, filter.Field);
    var nullValue = Expression.Constant(null, typeof(string));
    var exists = Expression.NotEqual(field, nullValue);
    Expression comparison;
    if (filter.Operation is "IsNull" or "IsNotNull" or "IsEmpty" or "IsNotEmpty") {
      comparison = filter.Operation switch {
        "IsNull" => Expression.Equal(field, nullValue),
        "IsNotNull" => exists,
        "IsEmpty" => Expression.Equal(field, Expression.Constant("")),
        _ => Expression.AndAlso(exists, Expression.NotEqual(field, Expression.Constant("")))
      };
      if (parentExists != null) return filter.Operation == "IsNull" ? Expression.OrElse(Expression.Not(parentExists), comparison) : Expression.AndAlso(parentExists, comparison);
      return comparison;
    }
    var value = Expression.Constant((filter.Value ?? "").ToLowerInvariant());
    var lower = Expression.Call(field, nameof(string.ToLower), Type.EmptyTypes);
    comparison = filter.Operation switch {
      "Equals" => Expression.Equal(lower, value),
      "NotEquals" => Expression.NotEqual(lower, value),
      "Contains" => Expression.Call(lower, nameof(string.Contains), Type.EmptyTypes, value),
      "DoesNotContain" => Expression.Not(Expression.Call(lower, nameof(string.Contains), Type.EmptyTypes, value)),
      "StartsWith" => Expression.Call(lower, nameof(string.StartsWith), Type.EmptyTypes, value),
      "EndsWith" => Expression.Call(lower, nameof(string.EndsWith), Type.EmptyTypes, value),
      _ => throw new ArgumentException("Недопустимая операция фильтра сотрудников.")
    };
    comparison = Expression.AndAlso(exists, comparison);
    return parentExists == null ? comparison : Expression.AndAlso(parentExists, comparison);
  }
}