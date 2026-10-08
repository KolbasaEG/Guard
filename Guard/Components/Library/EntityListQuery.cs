using System.Linq.Expressions;
using System.Linq.Dynamic.Core;
using Radzen;
using Radzen.Blazor;

namespace Guard.Components.Library;

public static class EntityListQuery<T>
{
  public static Func<IQueryable<T>, IQueryable<T>> Capture(RadzenDataFilter<T> filter,
      IReadOnlySet<string> fields, BrowserTimeService time)
  {
    var filters = (filter.Filters ?? []).Select(f => Clone(f, fields, time)).ToArray();
    var parameter = Expression.Parameter(typeof(T), "item");
    var count = 0;
    var condition = Build(filters, filter.LogicalFilterOperator, filter.FilterCaseSensitivity, parameter, 0, ref count);
    var predicate = Expression.Lambda<Func<T, bool>>(condition, parameter);
    return query => query.Where(predicate);
  }

  private static Expression Build(IEnumerable<CompositeFilterDescriptor> filters, LogicalFilterOperator logical,
      FilterCaseSensitivity sensitivity, ParameterExpression parameter, int depth, ref int count)
  {
    if (depth > 16) throw new ArgumentException("Слишком сложный фильтр.");
    Expression? result = null;
    foreach (var filter in filters) {
      if (++count > 100) throw new ArgumentException("Слишком сложный фильтр.");
      Expression condition;
      if (filter.Filters?.Any() == true) condition = Build(filter.Filters, filter.LogicalFilterOperator, sensitivity, parameter, depth + 1, ref count);
      else {
        if (string.IsNullOrWhiteSpace(filter.Property)) continue;
        Expression member = parameter;
        foreach (var segment in filter.Property.Split('.')) member = Expression.PropertyOrField(member, segment);
        var type = Nullable.GetUnderlyingType(member.Type) ?? member.Type;
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) {
          if (filter.FilterOperator is FilterOperator.IsNull or FilterOperator.IsNotNull) {
            if (!member.Type.IsGenericType) condition = Expression.Constant(filter.FilterOperator == FilterOperator.IsNotNull);
            else condition = filter.FilterOperator == FilterOperator.IsNull ? Expression.Equal(member, Expression.Constant(null, member.Type)) : Expression.NotEqual(member, Expression.Constant(null, member.Type));
          }
          else {
            if (filter.FilterValue == null) continue;
            var value = Expression.Convert(Expression.Constant(filter.FilterValue, type), member.Type);
            condition = filter.FilterOperator switch {
              FilterOperator.Equals => Expression.Equal(member, value), FilterOperator.NotEquals => Expression.NotEqual(member, value),
              FilterOperator.GreaterThan => Expression.GreaterThan(member, value), FilterOperator.GreaterThanOrEquals => Expression.GreaterThanOrEqual(member, value),
              FilterOperator.LessThan => Expression.LessThan(member, value), FilterOperator.LessThanOrEquals => Expression.LessThanOrEqual(member, value),
              _ => throw new ArgumentException("Недопустимая операция фильтра даты.")
            };
          }
        }
        else {
          var source = Array.Empty<T>().AsQueryable();
          var expression = source.Where([filter], LogicalFilterOperator.And, sensitivity).Expression;
          if (ReferenceEquals(expression, source.Expression)) continue;
          var lambda = (LambdaExpression)((UnaryExpression)((MethodCallExpression)expression).Arguments[1]).Operand;
          condition = new ReplaceSource(lambda.Parameters[0], parameter).Visit(lambda.Body)!;
        }
      }
      result = result == null ? condition : logical == LogicalFilterOperator.And ? Expression.AndAlso(result, condition) : Expression.OrElse(result, condition);
    }
    return result ?? Expression.Constant(true);
  }

  private static CompositeFilterDescriptor Clone(CompositeFilterDescriptor filter, IReadOnlySet<string> fields, BrowserTimeService time)
  {
    foreach (var property in new[] { filter.Property, filter.FilterProperty })
      if (!string.IsNullOrEmpty(property) && !fields.Contains(property)) throw new ArgumentException("Недопустимое поле фильтра.");
    var value = filter.FilterValue is Array array ? array.Clone() : filter.FilterValue is IEnumerable<string> strings ? strings.ToArray() : filter.FilterValue;
    var clone = new CompositeFilterDescriptor {
      Property = filter.Property, FilterProperty = filter.FilterProperty, Type = filter.Type,
      FilterOperator = filter.FilterOperator, LogicalFilterOperator = filter.LogicalFilterOperator,
      FilterValue = value, Filters = filter.Filters?.Select(f => Clone(f, fields, time)).ToArray()
    };
    var propertyType = typeof(T);
    if (!string.IsNullOrWhiteSpace(filter.Property))
      foreach (var segment in filter.Property.Split('.')) propertyType = propertyType.GetProperty(segment)?.PropertyType ?? throw new ArgumentException("Недопустимое поле фильтра.");
    var isOffset = propertyType == typeof(DateTimeOffset) || propertyType == typeof(DateTimeOffset?);
    if (value is DateTime date && date.Kind != DateTimeKind.Utc) {
      if (date.TimeOfDay == TimeSpan.Zero && filter.FilterOperator is FilterOperator.Equals or FilterOperator.NotEquals) {
        object start = time.ToUtc(date.Date); object end = time.ToUtc(date.Date.AddDays(1));
        if (isOffset) {
          start = new DateTimeOffset((DateTime)start); end = new DateTimeOffset((DateTime)end);
        }
        var equals = filter.FilterOperator == FilterOperator.Equals;
        return new CompositeFilterDescriptor {
          LogicalFilterOperator = equals ? LogicalFilterOperator.And : LogicalFilterOperator.Or,
          Filters = [
            new() { Property = filter.Property, Type = filter.Type, FilterOperator = equals ? FilterOperator.GreaterThanOrEquals : FilterOperator.LessThan, FilterValue = start },
            new() { Property = filter.Property, Type = filter.Type, FilterOperator = equals ? FilterOperator.LessThan : FilterOperator.GreaterThanOrEquals, FilterValue = end }
          ]
        };
      }
      var boundary = date.TimeOfDay == TimeSpan.Zero && filter.FilterOperator is FilterOperator.GreaterThan or FilterOperator.LessThanOrEquals ? date.AddDays(1) : date;
      if (date.TimeOfDay == TimeSpan.Zero && filter.FilterOperator == FilterOperator.GreaterThan) clone.FilterOperator = FilterOperator.GreaterThanOrEquals;
      if (date.TimeOfDay == TimeSpan.Zero && filter.FilterOperator == FilterOperator.LessThanOrEquals) clone.FilterOperator = FilterOperator.LessThan;
      clone.FilterValue = isOffset ? new DateTimeOffset(time.ToUtc(boundary)) : time.ToUtc(boundary);
    }
    if (value is DateTimeOffset offset) clone.FilterValue = offset.ToUniversalTime();
    return clone;
  }

  public static IQueryable<T> Sort(IQueryable<T> query, string? orderBy, IReadOnlySet<string> fields, string fallback, string? uniqueKey = "Id")
  {
    var clauses = new List<string>();
    foreach (var clause in (string.IsNullOrWhiteSpace(orderBy) ? fallback : orderBy).Split(',')) {
      var parts = clause.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length is < 1 or > 2 || !fields.Contains(parts[0])) throw new ArgumentException("Недопустимое поле сортировки.");
      var direction = parts.Length == 2 ? parts[1].ToLowerInvariant() : "asc";
      if (direction is not ("asc" or "desc")) throw new ArgumentException("Недопустимое направление сортировки.");
      clauses.Add($"{parts[0]} {direction}");
    }
    if (uniqueKey != null) clauses.Add(uniqueKey + " asc");
    return query.OrderBy(string.Join(", ", clauses));
  }
  private sealed class ReplaceSource(Expression original, Expression replacement) : ExpressionVisitor
  {
    public override Expression? Visit(Expression? node) => ReferenceEquals(node, original) ? replacement : base.Visit(node);
  }
}
