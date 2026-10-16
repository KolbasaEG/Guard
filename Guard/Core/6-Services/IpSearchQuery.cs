using System.Collections.Frozen;
using System.Linq.Expressions;
using System.Text.Json;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services.Exports;

namespace Guard.Core.Services;

// Только серверное построение запроса; не зависит от Radzen или Dynamic LINQ.
public static class IpSearchQuery
{
  private static readonly FrozenSet<string> Fields = IpSearchFilter.Fields;

  public static void ValidateFilter(ExportFilter? filter) => IpSearchFilter.Validate(filter);

  public static IQueryable<IpAddress> Filter(IQueryable<IpAddress> query, ExportFilter? filter) =>
    query.Where(Predicate(filter));

  private static Expression<Func<IpAddress, bool>> Predicate(ExportFilter? filter)
  {
    var parameter = Expression.Parameter(typeof(IpAddress), "ip");
    var count = 0;
    Expression Build(ExportFilter node, int depth) {
      if (depth > 16 || ++count > 100) throw new ArgumentException("Слишком сложный фильтр.");
      if (node.Children != null) {
        Expression? group = null;
        foreach (var child in node.Children) {
          if (child == null) throw new ArgumentException("Некорректное условие фильтра.");
          var expression = Build(child, depth + 1);
          group = group == null ? expression : node.All ? Expression.AndAlso(group, expression) : Expression.OrElse(group, expression);
        }
        return group ?? Expression.Constant(true);
      }
      if (string.IsNullOrWhiteSpace(node.Field)) return Expression.Constant(true);
      if (!Fields.Contains(node.Field)) throw new ArgumentException("Недопустимое поле фильтра.");
      Expression field;
      if (node.Field == "Subdivision.Name") {
        var subdivision = Expression.Property(parameter, nameof(IpAddress.Subdivision));
        field = Expression.Condition(Expression.Equal(subdivision, Expression.Constant(null, typeof(Subdivision))),
          Expression.Constant(null, typeof(string)), Expression.Property(subdivision, nameof(Subdivision.Name)));
      }
      else field = Expression.Property(parameter, node.Field);
      var nil = Expression.Constant(null, typeof(string));
      var exists = Expression.NotEqual(field, nil);
      var lower = Expression.Call(field, nameof(string.ToLower), Type.EmptyTypes);
      if (node.Operation is "IsNull" or "IsNotNull" or "IsEmpty" or "IsNotEmpty") return node.Operation switch {
        "IsNull" => Expression.Equal(field, nil),
        "IsNotNull" => exists,
        "IsEmpty" => Expression.Equal(field, Expression.Constant("")),
        _ => Expression.NotEqual(field, Expression.Constant(""))
      };
      if (node.Operation is "In" or "NotIn") {
        if (node.Value is null or { ValueKind: JsonValueKind.Null }) return Expression.Constant(true);
        if (node.Value is not { ValueKind: JsonValueKind.Array } array || array.GetArrayLength() > 1024)
          throw new ArgumentException("Недопустимый список значений фильтра.");
        var values = array.EnumerateArray().Select(value => value.ValueKind == JsonValueKind.String
          ? value.GetString()! : throw new ArgumentException("Ожидаются строковые значения фильтра.")).ToArray();
        var contains = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(string)], Expression.Constant(values), field);
        return node.Operation == "In" ? contains : Expression.Not(contains);
      }
      if (node.Value is { ValueKind: not (JsonValueKind.String or JsonValueKind.Null) })
        throw new ArgumentException("Ожидается строковое значение фильтра.");
      var text = node.Value is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
      var constant = Expression.Constant(text?.ToLowerInvariant(), typeof(string));
      if (node.Operation is not ("Equals" or "NotEquals" or "Contains" or "DoesNotContain" or "StartsWith" or "EndsWith"))
        throw new ArgumentException("Недопустимая операция фильтра.");
      if (string.IsNullOrEmpty(text)) return Expression.Constant(true); // Незаданный строковый фильтр не применяется.
      if (node.Operation is "Equals" or "NotEquals") {
        var equal = Expression.AndAlso(exists, Expression.Equal(lower, constant));
        return node.Operation == "Equals" ? equal : Expression.Not(equal);
      }
      var method = node.Operation == "DoesNotContain" ? nameof(string.Contains) : node.Operation;
      var call = Expression.Call(lower, method, Type.EmptyTypes, constant);
      return node.Operation == "DoesNotContain" ? Expression.OrElse(Expression.Not(exists), Expression.Not(call)) : Expression.AndAlso(exists, call);
    }
    return Expression.Lambda<Func<IpAddress, bool>>(filter == null ? Expression.Constant(true) : Build(filter, 0), parameter);
  }

  public static IQueryable<IpAddress> ApplyVisibility(IQueryable<IpAddress> query, DataViewMode mode,
    bool allRecords, bool isRoot, string? ownPath, SubdivisionHierarchyMode hierarchy)
  {
    if (!Enum.IsDefined(mode) || !Enum.IsDefined(hierarchy)) throw new ArgumentException("Неизвестный режим отображения.");
    if (allRecords && !isRoot) throw new UnauthorizedAccessException("Все IP-адреса доступны только Root.");
    query = ExportQueries.Statuses(query, mode);
    if (allRecords) return query;
    if (!ExportAccess.ValidPath(ownPath)) return query.Where(p => false);
    return hierarchy == SubdivisionHierarchyMode.IncludeChildren
      ? query.Where(p => p.Subdivision != null && p.Subdivision.Path.StartsWith(ownPath!))
      : query.Where(p => p.Subdivision != null && p.Subdivision.Path == ownPath);
  }

  public static IOrderedQueryable<IpAddress> Sort(IQueryable<IpAddress> query, string? orderBy)
  {
    if (string.IsNullOrWhiteSpace(orderBy)) return query.OrderByDescending(p => p.InsertedDate).ThenBy(p => p.Id);
    IOrderedQueryable<IpAddress>? ordered = null;
    var clauses = orderBy.Split(',');
    if (clauses.Length > 3) throw new ArgumentException("Слишком много полей сортировки.");
    foreach (var clause in clauses) {
      var parts = clause.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length is < 1 or > 2 || !Fields.Contains(parts[0])) throw new ArgumentException("Недопустимая сортировка.");
      var direction = parts.Length == 2 ? parts[1].ToLowerInvariant() : "asc";
      if (direction is not ("asc" or "desc")) throw new ArgumentException("Недопустимая сортировка.");
      Expression<Func<IpAddress, string?>> field = parts[0] switch {
        "Address" => p => p.Address,
        "Description" => p => p.Description,
        _ => p => p.Subdivision == null ? null : p.Subdivision.Name
      };
      ordered = ordered == null ? direction == "asc" ? query.OrderBy(field) : query.OrderByDescending(field)
        : direction == "asc" ? ordered.ThenBy(field) : ordered.ThenByDescending(field);
    }
    return ordered!.ThenBy(p => p.Id);
  }
}
