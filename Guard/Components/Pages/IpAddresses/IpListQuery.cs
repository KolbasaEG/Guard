using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Extensions;
using Guard.Core.Services.Exports;
using Radzen;
using Radzen.Blazor;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;

namespace Guard.Components.Pages.IpAddresses;

// Снимок условий фильтра. После «Применить» правки конструктора не меняют запрос.
public static class IpListQuery
{
  private static readonly HashSet<string> Fields = ["Subdivision.Name", "Address", "Description"];

  public static Func<IQueryable<IpAddress>, IQueryable<IpAddress>> Capture(RadzenDataFilter<IpAddress> filter)
  {
    var count = 0;
    var filters = new List<CompositeFilterDescriptor>();
    foreach (var descriptor in filter.Filters ?? []) filters.Add(Clone(descriptor, 0, ref count));
    var source = Array.Empty<IpAddress>().AsQueryable();
    var expression = source.Where(filters, filter.LogicalFilterOperator, filter.FilterCaseSensitivity).Expression;
    return query => query.Provider.CreateQuery<IpAddress>(new ReplaceSource(source.Expression, query.Expression).Visit(expression)!);
  }

  private static CompositeFilterDescriptor Clone(CompositeFilterDescriptor filter, int depth, ref int count)
  {
    if (depth > 16 || ++count > 256) throw new ArgumentException("Слишком сложные условия фильтра.");
    if (!string.IsNullOrEmpty(filter.Property) && !Fields.Contains(filter.Property) || !string.IsNullOrEmpty(filter.FilterProperty))
      throw new ArgumentException("Недопустимое поле фильтра.");
    if (filter.FilterOperator is { } operation && !Enum.IsDefined(operation) || !Enum.IsDefined(filter.LogicalFilterOperator))
      throw new ArgumentException("Недопустимая операция фильтра.");
    if (filter.FilterValue is not null and not string and not IEnumerable<string>)
      throw new ArgumentException("Недопустимое значение фильтра.");
    var children = new List<CompositeFilterDescriptor>();
    foreach (var child in filter.Filters ?? []) children.Add(Clone(child, depth + 1, ref count));
    return new() {
      Property = filter.Property, Type = filter.Type, FilterOperator = filter.FilterOperator,
      LogicalFilterOperator = filter.LogicalFilterOperator,
      FilterValue = filter.FilterValue is IEnumerable<string> values ? values.ToArray() : filter.FilterValue,
      Filters = filter.Filters == null ? null : children.ToArray()
    };
  }

  public static IQueryable<IpAddress> ApplyVisibility(IQueryable<IpAddress> query, DataViewMode mode,
    bool allRecords, bool isRoot, string? ownPath, SubdivisionHierarchyMode hierarchy)
  {
    if (!Enum.IsDefined(mode) || !Enum.IsDefined(hierarchy)) throw new ArgumentException("Неизвестный режим отображения.");
    if (allRecords && !isRoot) throw new UnauthorizedAccessException("Все IP-адреса доступны только Root.");
    query = query.FilterByMode(mode);
    if (allRecords) return query;
    return !ExportAccess.ValidPath(ownPath) ? query.Where(p => false) : query.FilterBySubdivision(ownPath, hierarchy);
  }

  public static IQueryable<IpAddress> Sort(IQueryable<IpAddress> query, string? orderBy)
  {
    if (string.IsNullOrWhiteSpace(orderBy)) return query.OrderByDescending(p => p.InsertedDate).ThenBy(p => p.Id);
    var clauses = new List<string>();
    foreach (var clause in orderBy.Split(',')) {
      var parts = clause.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
      if (parts.Length is < 1 or > 2 || !Fields.Contains(parts[0])) throw new ArgumentException("Недопустимая сортировка.");
      var direction = parts.Length == 2 ? parts[1].ToLowerInvariant() : "asc";
      if (direction is not ("asc" or "desc")) throw new ArgumentException("Недопустимая сортировка.");
      clauses.Add($"{parts[0]} {direction}");
    }
    clauses.Add("Id asc");
    return query.OrderBy(string.Join(", ", clauses));
  }

  private sealed class ReplaceSource(Expression original, Expression replacement) : ExpressionVisitor
  {
    public override Expression? Visit(Expression? node) => ReferenceEquals(node, original) ? replacement : base.Visit(node);
  }
}
