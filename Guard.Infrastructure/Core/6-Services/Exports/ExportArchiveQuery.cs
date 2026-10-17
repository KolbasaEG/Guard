using System.Collections.Frozen;
using Guard.Core.Enums;

namespace Guard.Core.Services.Exports;

public static class ExportArchiveQuery
{
  private static readonly FrozenDictionary<string, Type> FieldTypes = new Dictionary<string, Type> {
    ["Id"] = typeof(Guid), ["AuthorName"] = typeof(string), ["ClientIp"] = typeof(string),
    ["Area"] = typeof(ExportArea), ["Format"] = typeof(ExportFormat), ["State"] = typeof(ExportJobState),
    ["CreatedAtUtc"] = typeof(DateTimeOffset), ["ExpiresAtUtc"] = typeof(DateTimeOffset), ["RowCount"] = typeof(long)
  }.ToFrozenDictionary(StringComparer.Ordinal);

  public static IReadOnlySet<string> Fields { get; } = FieldTypes.Keys.ToFrozenSet(StringComparer.Ordinal);
  public static void ValidateFilter(ExportFilter? filter) => ExportFilterValidation.Validate(filter, FieldTypes);
}
