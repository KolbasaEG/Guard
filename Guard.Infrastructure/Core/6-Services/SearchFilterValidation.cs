using System.Collections.Frozen;
using System.Text.Json;
using Guard.Core.Services.Exports;

namespace Guard.Core.Services;

// Serialized search contract; no entity reflection, query provider or UI descriptors.
public static class SearchFilterValidation
{
  public static void Validate(ExportFilter? filter, IReadOnlyDictionary<string, Type> fields)
  {
    var count = 0;
    void Visit(ExportFilter node, int depth) {
      if (node == null) throw new ArgumentException("Некорректное условие фильтра.");
      if (depth > 16 || ++count > 100) throw new ArgumentException("Слишком сложный фильтр.");
      if (node.Children != null) {
        foreach (var child in node.Children) Visit(child, depth + 1);
        return;
      }
      if (string.IsNullOrWhiteSpace(node.Field)) return;
      if (!fields.TryGetValue(node.Field, out var type)) throw new ArgumentException("Недопустимое поле поиска.");
      if (type == typeof(string)) {
        if (node.Operation is "IsNull" or "IsNotNull" or "IsEmpty" or "IsNotEmpty") return;
        if (node.Operation is "In" or "NotIn") {
          if (node.Value is null or { ValueKind: JsonValueKind.Null }) return;
          if (node.Value is not { ValueKind: JsonValueKind.Array } array || array.GetArrayLength() > 1024)
            throw new ArgumentException("Недопустимый список значений фильтра.");
          foreach (var value in array.EnumerateArray())
            if (value.ValueKind != JsonValueKind.String) throw new ArgumentException("Ожидаются строковые значения фильтра.");
          return;
        }
        if (node.Operation is not ("Equals" or "NotEquals" or "Contains" or "DoesNotContain" or "StartsWith" or "EndsWith"))
          throw new ArgumentException("Недопустимая операция строкового фильтра.");
        if (node.Value is { ValueKind: not (JsonValueKind.String or JsonValueKind.Null) })
          throw new ArgumentException("Ожидается строковое значение.");
        return;
      }
      var valueType = Nullable.GetUnderlyingType(type) ?? type;
      if (node.Operation is "IsNull" or "IsNotNull") return;
      if (node.Operation is not ("Equals" or "NotEquals" or "GreaterThan" or "GreaterThanOrEquals" or "LessThan" or "LessThanOrEquals") ||
          (valueType == typeof(bool) || valueType == typeof(Guid)) && node.Operation is not ("Equals" or "NotEquals"))
        throw new ArgumentException("Недопустимая операция фильтра поиска.");
      if (node.Value is null or { ValueKind: JsonValueKind.Null }) return;
      object scalar;
      try { scalar = node.Value.Value.Deserialize(valueType) ?? throw new ArgumentException("Ожидается значение фильтра."); }
      catch (JsonException ex) { throw new ArgumentException("Недопустимое значение фильтра.", ex); }
      if (scalar is double number && !double.IsFinite(number) || scalar is DateTime date && date.Kind != DateTimeKind.Utc || scalar is DateTimeOffset offset && offset.Offset != TimeSpan.Zero)
        throw new ArgumentException("Ожидается конечное число или дата UTC.");
    }
    if (filter != null) Visit(filter, 0);
  }
}

public static class UserSearchFilter
{
  internal static readonly FrozenDictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal) {
    ["Id"] = typeof(string), ["UserName"] = typeof(string), ["Email"] = typeof(string),
    ["PersonalId"] = typeof(Guid?), ["AccountBlockReason"] = typeof(string),
    ["LastActivityAtUtc"] = typeof(DateTimeOffset?), ["MustChangePassword"] = typeof(bool)
  }.ToFrozenDictionary(StringComparer.Ordinal);
  public static readonly FrozenSet<string> Fields = Types.Keys.ToFrozenSet(StringComparer.Ordinal);
  public static void Validate(ExportFilter? filter) => SearchFilterValidation.Validate(filter, Types);
}

public static class RoleSearchFilter
{
  internal static readonly FrozenDictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal) {
    ["Id"] = typeof(string), ["Name"] = typeof(string)
  }.ToFrozenDictionary(StringComparer.Ordinal);
  public static readonly FrozenSet<string> Fields = Types.Keys.ToFrozenSet(StringComparer.Ordinal);
  public static void Validate(ExportFilter? filter) => SearchFilterValidation.Validate(filter, Types);
}

public static class LogSearchFilter
{
  internal static readonly FrozenDictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal) {
    ["Timestamp"] = typeof(DateTime), ["Level"] = typeof(string), ["Layer"] = typeof(string),
    ["UserId"] = typeof(string), ["ClientIp"] = typeof(string), ["Message"] = typeof(string), ["Exception"] = typeof(string)
  }.ToFrozenDictionary(StringComparer.Ordinal);
  public static readonly FrozenSet<string> Fields = Types.Keys.ToFrozenSet(StringComparer.Ordinal);
  public static void Validate(ExportFilter? filter) => SearchFilterValidation.Validate(filter, Types);
}

public static class IpSearchFilter
{
  internal static readonly FrozenDictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal) {
    ["Address"] = typeof(string), ["Description"] = typeof(string), ["Subdivision.Name"] = typeof(string)
  }.ToFrozenDictionary(StringComparer.Ordinal);
  public static readonly FrozenSet<string> Fields = Types.Keys.ToFrozenSet(StringComparer.Ordinal);
  public static void Validate(ExportFilter? filter) => SearchFilterValidation.Validate(filter, Types);
}

public static class SubdivisionSearchFilter
{
  internal static readonly FrozenDictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal) {
    ["Address"] = typeof(string), ["Fax"] = typeof(string), ["Id"] = typeof(Guid),
    ["InsertedDate"] = typeof(DateTime), ["IsDepartment"] = typeof(bool), ["LevelOrder"] = typeof(int),
    ["Name"] = typeof(string), ["OrganType.Value"] = typeof(string), ["Parent.Name"] = typeof(string),
    ["Path"] = typeof(string), ["Phone"] = typeof(string), ["PositionFormationName"] = typeof(string),
    ["PostalCode"] = typeof(string), ["StaffCount"] = typeof(double), ["StatusClassifier.Value"] = typeof(string)
  }.ToFrozenDictionary(StringComparer.Ordinal);
  public static readonly FrozenSet<string> Fields = Types.Keys.ToFrozenSet(StringComparer.Ordinal);
  public static void Validate(ExportFilter? filter) => SearchFilterValidation.Validate(filter, Types);
}

public static class ClassifierSearchFilter
{
  internal static readonly FrozenDictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal) {
    ["ClassifierName"] = typeof(string), ["Code"] = typeof(int), ["Id"] = typeof(int),
    ["IsActive"] = typeof(bool), ["Type"] = typeof(int), ["UpdatedAt"] = typeof(DateTime), ["Value"] = typeof(string)
  }.ToFrozenDictionary(StringComparer.Ordinal);
  public static readonly FrozenSet<string> Fields = Types.Keys.ToFrozenSet(StringComparer.Ordinal);
  public static void Validate(ExportFilter? filter) => SearchFilterValidation.Validate(filter, Types);
}
