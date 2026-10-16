using Guard.Core.Services.Exports;
using Radzen;
using Radzen.Blazor;

namespace Guard.Components.Library;

public static class AdministrationListQuery
{
  public static ExportFilter Capture<T>(RadzenDataFilter<T> filter, BrowserTimeService time, IReadOnlySet<string> fields, Action<ExportFilter?> validate)
  {
    // Проверка сложности до рекурсивного преобразования дат в Web.
    if (!Enum.IsDefined(filter.LogicalFilterOperator)) throw new ArgumentException("Недопустимая операция фильтра.");
    var count = 0;
    void Validate(CompositeFilterDescriptor descriptor, int depth) {
      if (descriptor == null) throw new ArgumentException("Некорректное условие фильтра.");
      if (depth > 16 || ++count > 100) throw new ArgumentException("Слишком сложный фильтр.");
      if (!Enum.IsDefined(descriptor.LogicalFilterOperator) || descriptor.FilterOperator is { } op && !Enum.IsDefined(op))
        throw new ArgumentException("Недопустимая операция фильтра.");
      foreach (var child in descriptor.Filters ?? []) Validate(child, depth + 1);
    }
    foreach (var descriptor in filter.Filters ?? []) Validate(descriptor, 0);
    var snapshot = ExportFilterCapture.Capture(EntityListQuery<T>.Snapshot(filter, fields, time), filter.LogicalFilterOperator);
    validate(snapshot);
    return snapshot;
  }
}
