using Radzen;
using Radzen.Blazor;

namespace Guard.Components.Library;

public static class EntityListQuery<T>
{
  public static CompositeFilterDescriptor[] Snapshot(RadzenDataFilter<T> filter, IReadOnlySet<string> fields, BrowserTimeService time) =>
    (filter.Filters ?? []).Select(f => Clone(f, fields, time)).ToArray();
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
}
