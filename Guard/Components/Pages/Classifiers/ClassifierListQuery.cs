using Guard.Components.Library;
using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Radzen;
using Radzen.Blazor;

namespace Guard.Components.Pages.Classifiers;

public static class ClassifierListQuery
{
  public static ExportFilter Capture(RadzenDataFilter<Classifier> filter, BrowserTimeService time)
  {
    // Проверка сложности до рекурсивного преобразования дат в Web.
    if (!Enum.IsDefined(filter.LogicalFilterOperator)) throw new ArgumentException("Недопустимая операция фильтра.");
    var count = 0;
    void Validate(CompositeFilterDescriptor descriptor, int depth) {
      if (depth > 16 || ++count > 100) throw new ArgumentException("Слишком сложный фильтр.");
      if (!Enum.IsDefined(descriptor.LogicalFilterOperator) || descriptor.FilterOperator is { } op && !Enum.IsDefined(op))
        throw new ArgumentException("Недопустимая операция фильтра.");
      foreach (var child in descriptor.Filters ?? []) Validate(child, depth + 1);
    }
    foreach (var descriptor in filter.Filters ?? []) Validate(descriptor, 0);
    var snapshot = ExportFilterCapture.Capture(EntityListQuery<Classifier>.Snapshot(filter, ClassifierSearchQuery.Fields, time), filter.LogicalFilterOperator);
    ClassifierSearchQuery.ValidateFilter(snapshot);
    return snapshot;
  }
}
