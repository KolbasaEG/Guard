using System.Linq.Expressions;
using System.Text.Json;
using Radzen;

namespace Guard.Core.Services.Exports;

// Same Radzen predicate semantics as the lists, with a serializable independent snapshot.
public static class ExportFilters
{
  public static ExportFilter Capture(IEnumerable<CompositeFilterDescriptor> filters, LogicalFilterOperator logical)
  {
    var count = 0;
    ExportFilter Copy(CompositeFilterDescriptor f, int depth) {
      if (depth > 16 || ++count > 100) throw new ArgumentException("Слишком сложный фильтр.");
      if (!string.IsNullOrWhiteSpace(f.FilterProperty) && f.FilterProperty != f.Property)
        throw new ArgumentException("Недопустимое поле фильтра.");
      var children = f.Filters?.ToArray();
      if (children?.Length > 0) return new(All: f.LogicalFilterOperator == LogicalFilterOperator.And, Children: children.Select(c => Copy(c, depth + 1)).ToArray());
      return new(f.Property, f.FilterOperator.ToString(), f.FilterValue == null ? null : JsonSerializer.SerializeToElement(f.FilterValue, f.FilterValue.GetType()));
    }
    return new(All: logical == LogicalFilterOperator.And, Children: filters.Select(f => Copy(f, 0)).ToArray());
  }
  public static IQueryable<T> Apply<T>(IQueryable<T> query, ExportFilter? filter, IReadOnlySet<string> fields)
  {
    if (filter == null) return query;
    var count = 0;
    CompositeFilterDescriptor Build(ExportFilter f, int depth) {
      if (depth > 16 || ++count > 100) throw new ArgumentException("Слишком сложный фильтр.");
      if (f.Children != null) return new() { LogicalFilterOperator = f.All ? LogicalFilterOperator.And : LogicalFilterOperator.Or, Filters = f.Children.Select(c => Build(c, depth + 1)).ToArray() };
      if (string.IsNullOrWhiteSpace(f.Field)) return new();
      if (!fields.Contains(f.Field) || !Enum.TryParse<FilterOperator>(f.Operation, out var operation) || !Enum.IsDefined(operation))
        throw new ArgumentException("Недопустимое поле или операция фильтра.");
      var type = typeof(T);
      foreach (var segment in f.Field.Split('.')) type = type.GetProperty(segment)?.PropertyType ?? throw new ArgumentException("Неизвестное поле фильтра.");
      var array = f.Value is { ValueKind: JsonValueKind.Array };
      object? value = f.Value is not { } json || json.ValueKind == JsonValueKind.Null ? null :
        array && type == typeof(string) ? json.Deserialize<string[]>() : json.Deserialize(type);
      if (array && type != typeof(string)) throw new ArgumentException("Недопустимое значение фильтра.");
      return new() { Property = f.Field, Type = array ? typeof(IEnumerable<string>) : type, FilterOperator = operation, FilterValue = value };
    }
    var descriptor = Build(filter, 0);
    var source = Array.Empty<T>().AsQueryable();
    var expression = descriptor.Filters != null ? source.Where(descriptor.Filters, descriptor.LogicalFilterOperator, FilterCaseSensitivity.CaseInsensitive).Expression :
      source.Where([descriptor], LogicalFilterOperator.And, FilterCaseSensitivity.CaseInsensitive).Expression;
    return query.Provider.CreateQuery<T>(new ReplaceSource(source.Expression, query.Expression).Visit(expression)!);
  }
  private sealed class ReplaceSource(Expression original, Expression replacement) : ExpressionVisitor
  {
    public override Expression? Visit(Expression? node) => ReferenceEquals(node, original) ? replacement : base.Visit(node);
  }
}
