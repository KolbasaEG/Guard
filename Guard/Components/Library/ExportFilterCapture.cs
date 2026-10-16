using System.Text.Json;
using Guard.Core.Services.Exports;
using Radzen;

namespace Guard.Components.Library;

// Converts UI state into an independent serialized snapshot; query translation stays on the server.
public static class ExportFilterCapture
{
  public static ExportFilter Capture(IEnumerable<CompositeFilterDescriptor> filters, LogicalFilterOperator logical)
  {
    if (!Enum.IsDefined(logical)) throw new ArgumentException("Недопустимая операция фильтра.");
    var count = 0;
    ExportFilter Copy(CompositeFilterDescriptor f, int depth) {
      if (f == null) throw new ArgumentException("Некорректное условие фильтра.");
      if (depth > 16 || ++count > 100) throw new ArgumentException("Слишком сложный фильтр.");
      if (!Enum.IsDefined(f.LogicalFilterOperator) || f.FilterOperator is { } op && !Enum.IsDefined(op))
        throw new ArgumentException("Недопустимая операция фильтра.");
      if (!string.IsNullOrWhiteSpace(f.FilterProperty) && f.FilterProperty != f.Property)
        throw new ArgumentException("Недопустимое поле фильтра.");
      var children = f.Filters?.ToArray();
      if (children?.Length > 0) return new(All: f.LogicalFilterOperator == LogicalFilterOperator.And, Children: children.Select(c => Copy(c, depth + 1)).ToArray());
      return new(f.Property, f.FilterOperator.ToString(), f.FilterValue == null ? null : JsonSerializer.SerializeToElement(f.FilterValue, f.FilterValue.GetType()));
    }
    return new(All: logical == LogicalFilterOperator.And, Children: filters.Select(f => Copy(f, 0)).ToArray());
  }
}
