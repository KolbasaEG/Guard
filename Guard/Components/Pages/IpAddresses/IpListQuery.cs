using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Radzen;
using Radzen.Blazor;
using System.Text.Json;

namespace Guard.Components.Pages.IpAddresses;

// Web-адаптер: копирует условия Radzen в независимый сериализуемый контракт.
public static class IpListQuery
{
  public static ExportFilter Capture(RadzenDataFilter<IpAddress> filter)
  {
    if (!Enum.IsDefined(filter.LogicalFilterOperator)) throw new ArgumentException("Недопустимая операция фильтра.");
    var count = 0;
    ExportFilter Copy(CompositeFilterDescriptor descriptor, int depth) {
      if (depth > 16 || ++count > 100) throw new ArgumentException("Слишком сложный фильтр.");
      if (!string.IsNullOrEmpty(descriptor.FilterProperty) || !Enum.IsDefined(descriptor.LogicalFilterOperator) ||
          descriptor.FilterOperator is { } operation && !Enum.IsDefined(operation))
        throw new ArgumentException("Недопустимая операция фильтра.");
      var children = descriptor.Filters?.ToArray();
      if (children?.Length > 0) return new(All: descriptor.LogicalFilterOperator == LogicalFilterOperator.And,
        Children: children.Select(child => Copy(child, depth + 1)).ToArray());
      return new(descriptor.Property, descriptor.FilterOperator.ToString(), descriptor.FilterValue == null ? null
        : JsonSerializer.SerializeToElement(descriptor.FilterValue, descriptor.FilterValue.GetType()));
    }
    var snapshot = new ExportFilter(All: filter.LogicalFilterOperator == LogicalFilterOperator.And,
      Children: (filter.Filters ?? []).Select(descriptor => Copy(descriptor, 0)).ToArray());
    IpSearchQuery.ValidateFilter(snapshot);
    return snapshot;
  }
}
