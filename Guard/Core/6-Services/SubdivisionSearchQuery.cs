using System.Collections.Frozen;
using System.Linq.Expressions;
using System.Text.Json;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services.Exports;

namespace Guard.Core.Services;

public static class SubdivisionSearchQuery
{
  public static readonly FrozenSet<string> Fields = SubdivisionSearchFilter.Fields;

  private static Expression Field(ParameterExpression parameter, string name)
  {
    if (!Fields.Contains(name)) throw new ArgumentException("Недопустимое поле подразделения.");
    var parts = name.Split('.');
    var member = Expression.Property(parameter, parts[0]);
    return parts.Length == 1 ? member : Expression.Condition(
      Expression.Equal(member, Expression.Constant(null, member.Type)), Expression.Constant(null, typeof(string)),
      Expression.Property(member, parts[1]));
  }

  public static void ValidateFilter(ExportFilter? filter) => SubdivisionSearchFilter.Validate(filter);
  public static IQueryable<Subdivision> Filter(IQueryable<Subdivision> query, ExportFilter? filter) => query.Where(Predicate(filter));

  private static Expression<Func<Subdivision, bool>> Predicate(ExportFilter? filter)
  {
    var parameter = Expression.Parameter(typeof(Subdivision), "s");
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
      var field = Field(parameter, node.Field);
      if (field.Type == typeof(string)) return Text(field, node);
      if (node.Operation is "IsNull" or "IsNotNull") return Expression.Constant(node.Operation == "IsNotNull");
      if (node.Operation is not ("Equals" or "NotEquals" or "GreaterThan" or "GreaterThanOrEquals" or "LessThan" or "LessThanOrEquals") ||
          (field.Type == typeof(bool) || field.Type == typeof(Guid)) && node.Operation is not ("Equals" or "NotEquals"))
        throw new ArgumentException("Недопустимая операция фильтра подразделений.");
      if (node.Value is null or { ValueKind: JsonValueKind.Null }) return Expression.Constant(true);
      object value;
      try { value = node.Value.Value.Deserialize(field.Type) ?? throw new ArgumentException("Ожидается значение фильтра."); }
      catch (JsonException ex) { throw new ArgumentException("Недопустимое значение фильтра.", ex); }
      if (value is double number && !double.IsFinite(number) || value is DateTime date && date.Kind != DateTimeKind.Utc)
        throw new ArgumentException("Ожидается конечное число или дата UTC.");
      var constant = Expression.Constant(value, field.Type);
      return node.Operation switch {
        "Equals" => Expression.Equal(field, constant), "NotEquals" => Expression.NotEqual(field, constant),
        "GreaterThan" => Expression.GreaterThan(field, constant), "GreaterThanOrEquals" => Expression.GreaterThanOrEqual(field, constant),
        "LessThan" => Expression.LessThan(field, constant), _ => Expression.LessThanOrEqual(field, constant)
      };
    }
    return Expression.Lambda<Func<Subdivision, bool>>(filter == null ? Expression.Constant(true) : Build(filter, 0), parameter);
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

  public static IQueryable<Subdivision> Visibility(IQueryable<Subdivision> query, DataViewMode mode,
    bool all, bool isRoot, string? ownPath, SubdivisionHierarchyMode hierarchy)
  {
    if (!Enum.IsDefined(mode) || !Enum.IsDefined(hierarchy)) throw new ArgumentException("Неизвестный режим отображения.");
    if (all && !isRoot) throw new UnauthorizedAccessException("Все подразделения доступны только Root.");
    query = ExportQueries.Statuses(query, mode);
    if (all) return query;
    if (!ExportAccess.ValidPath(ownPath)) return query.Where(s => false);
    return hierarchy == SubdivisionHierarchyMode.CurrentOnly ? query.Where(s => s.Path == ownPath) : query.Where(s => s.Path.StartsWith(ownPath!));
  }

  public static IOrderedQueryable<Subdivision> Sort(IQueryable<Subdivision> query, string? orderBy)
  {
    if (string.IsNullOrWhiteSpace(orderBy)) return query.OrderByDescending(s => s.InsertedDate).ThenBy(s => s.Id);
    var clauses = orderBy.Split(',');
    if (clauses.Length > Fields.Count) throw new ArgumentException("Слишком много полей сортировки.");
    var parameter = Expression.Parameter(typeof(Subdivision), "s");
    var expression = query.Expression;
    var first = true;
    foreach (var clause in clauses) {
      var parts = clause.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length is < 1 or > 2) throw new ArgumentException("Недопустимая сортировка.");
      var field = Field(parameter, parts[0]);
      var direction = parts.Length == 1 ? "asc" : parts[1].ToLowerInvariant();
      if (direction is not ("asc" or "desc")) throw new ArgumentException("Недопустимое направление сортировки.");
      var method = first ? direction == "asc" ? "OrderBy" : "OrderByDescending" : direction == "asc" ? "ThenBy" : "ThenByDescending";
      expression = Expression.Call(typeof(Queryable), method, [typeof(Subdivision), field.Type], expression,
        Expression.Quote(Expression.Lambda(field, parameter)));
      first = false;
    }
    return ((IOrderedQueryable<Subdivision>)query.Provider.CreateQuery<Subdivision>(expression)).ThenBy(s => s.Id);
  }
}
