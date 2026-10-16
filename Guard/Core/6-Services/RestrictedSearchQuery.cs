using System.Linq.Expressions;
using System.Text.Json;
using Guard.Core.Services.Exports;

namespace Guard.Core.Services;

internal static class RestrictedSearchQuery<T>
{

  private static Expression Field(ParameterExpression parameter, string name, IReadOnlySet<string> fields)
  {
    if (!fields.Contains(name)) throw new ArgumentException("Недопустимое поле поиска.");
    return Expression.Property(parameter, name);
  }

  public static IQueryable<T> Filter(IQueryable<T> query, ExportFilter? filter, IReadOnlySet<string> fields) => query.Where(Predicate(filter, fields));

  private static Expression<Func<T, bool>> Predicate(ExportFilter? filter, IReadOnlySet<string> fields)
  {
    var parameter = Expression.Parameter(typeof(T), "s");
    var count = 0;
    Expression Build(ExportFilter node, int depth) {
      if (depth > 16 || ++count > 100) throw new ArgumentException("Слишком сложный фильтр.");
      if (node.Children != null) {
        Expression? group = null;
        foreach (var child in node.Children) {
          if (child == null) throw new ArgumentException("Некорректное условие фильтра.");
          var item = Build(child, depth + 1);
          group = group == null ? item : node.All ? Expression.AndAlso(group, item) : Expression.OrElse(group, item);
        }
        return group ?? Expression.Constant(true);
      }
      if (string.IsNullOrWhiteSpace(node.Field)) return Expression.Constant(true);
      var field = Field(parameter, node.Field, fields);
      if (field.Type == typeof(string)) return Text(field, node);
      var valueType = Nullable.GetUnderlyingType(field.Type) ?? field.Type;
      if (node.Operation is "IsNull" or "IsNotNull") return valueType != field.Type
        ? node.Operation == "IsNull" ? Expression.Equal(field, Expression.Constant(null, field.Type)) : Expression.NotEqual(field, Expression.Constant(null, field.Type))
        : Expression.Constant(node.Operation == "IsNotNull");
      if (node.Operation is not ("Equals" or "NotEquals" or "GreaterThan" or "GreaterThanOrEquals" or "LessThan" or "LessThanOrEquals") ||
          (valueType == typeof(bool) || valueType == typeof(Guid)) && node.Operation is not ("Equals" or "NotEquals"))
        throw new ArgumentException("Недопустимая операция фильтра поиска.");
      if (node.Value is null or { ValueKind: JsonValueKind.Null }) return Expression.Constant(true);
      object value;
      try { value = node.Value.Value.Deserialize(valueType) ?? throw new ArgumentException("Ожидается значение фильтра."); }
      catch (JsonException ex) { throw new ArgumentException("Недопустимое значение фильтра.", ex); }
      if (value is double number && !double.IsFinite(number) || value is DateTime date && date.Kind != DateTimeKind.Utc || value is DateTimeOffset offset && offset.Offset != TimeSpan.Zero)
        throw new ArgumentException("Ожидается конечное число или дата UTC.");
      Expression constant = Expression.Constant(value, valueType);
      if (valueType != field.Type) constant = Expression.Convert(constant, field.Type);
      return node.Operation switch {
        "Equals" => Expression.Equal(field, constant), "NotEquals" => Expression.NotEqual(field, constant),
        "GreaterThan" => Expression.GreaterThan(field, constant), "GreaterThanOrEquals" => Expression.GreaterThanOrEqual(field, constant),
        "LessThan" => Expression.LessThan(field, constant), _ => Expression.LessThanOrEqual(field, constant)
      };
    }
    return Expression.Lambda<Func<T, bool>>(filter == null ? Expression.Constant(true) : Build(filter, 0), parameter);
  }

  private static Expression Text(Expression field, ExportFilter node)
  {
    var nil = Expression.Constant(null, typeof(string));
    var exists = Expression.NotEqual(field, nil);
    if (node.Operation is "IsNull" or "IsNotNull" or "IsEmpty" or "IsNotEmpty") return node.Operation switch {
      "IsNull" => Expression.Equal(field, nil), "IsNotNull" => exists,
      "IsEmpty" => Expression.Equal(field, Expression.Constant("")), _ => Expression.NotEqual(field, Expression.Constant(""))
    };
    if (node.Operation is "In" or "NotIn") {
      if (node.Value is null or { ValueKind: JsonValueKind.Null }) return Expression.Constant(true);
      if (node.Value is not { ValueKind: JsonValueKind.Array } array || array.GetArrayLength() > 1024)
        throw new ArgumentException("Недопустимый список значений фильтра.");
      var values = array.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.String ? v.GetString()!
        : throw new ArgumentException("Ожидаются строковые значения фильтра.")).ToArray();
      var contains = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(string)], Expression.Constant(values), field);
      return node.Operation == "In" ? contains : Expression.Not(contains);
    }
    if (node.Operation is not ("Equals" or "NotEquals" or "Contains" or "DoesNotContain" or "StartsWith" or "EndsWith"))
      throw new ArgumentException("Недопустимая операция строкового фильтра.");
    if (node.Value is { ValueKind: not (JsonValueKind.String or JsonValueKind.Null) }) throw new ArgumentException("Ожидается строковое значение.");
    var text = node.Value is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
    if (string.IsNullOrEmpty(text)) return Expression.Constant(true);
    var lower = Expression.Call(field, nameof(string.ToLower), Type.EmptyTypes);
    var constant = Expression.Constant(text.ToLowerInvariant());
    if (node.Operation is "Equals" or "NotEquals") {
      var equal = Expression.AndAlso(exists, Expression.Equal(lower, constant));
      return node.Operation == "Equals" ? equal : Expression.Not(equal);
    }
    var method = node.Operation == "DoesNotContain" ? nameof(string.Contains) : node.Operation;
    var call = Expression.Call(lower, method, Type.EmptyTypes, constant);
    return node.Operation == "DoesNotContain" ? Expression.OrElse(Expression.Not(exists), Expression.Not(call)) : Expression.AndAlso(exists, call);
  }

  public static IOrderedQueryable<T> Sort(IQueryable<T> query, string? orderBy, IReadOnlySet<string> fields, string defaultOrder, params string[] tieBreakers)
  {
    if (string.IsNullOrWhiteSpace(orderBy)) orderBy = defaultOrder;
    var clauses = orderBy.Split(',');
    if (clauses.Length > fields.Count) throw new ArgumentException("Слишком много полей сортировки.");
    var parameter = Expression.Parameter(typeof(T), "s");
    var expression = query.Expression;
    var first = true;
    foreach (var clause in clauses) {
      var parts = clause.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length is < 1 or > 2) throw new ArgumentException("Недопустимая сортировка.");
      var field = Field(parameter, parts[0], fields);
      var direction = parts.Length == 1 ? "asc" : parts[1].ToLowerInvariant();
      if (direction is not ("asc" or "desc")) throw new ArgumentException("Недопустимое направление сортировки.");
      var method = first ? direction == "asc" ? "OrderBy" : "OrderByDescending" : direction == "asc" ? "ThenBy" : "ThenByDescending";
      expression = Expression.Call(typeof(Queryable), method, [typeof(T), field.Type], expression,
        Expression.Quote(Expression.Lambda(field, parameter)));
      first = false;
    }
    foreach (var name in tieBreakers) {
      var field = Field(parameter, name, fields);
      expression = Expression.Call(typeof(Queryable), "ThenBy", [typeof(T), field.Type], expression,
        Expression.Quote(Expression.Lambda(field, parameter)));
    }
    return (IOrderedQueryable<T>)query.Provider.CreateQuery<T>(expression);
  }
}
