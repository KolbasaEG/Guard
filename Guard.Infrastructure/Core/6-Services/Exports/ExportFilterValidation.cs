using System.Text.Json;

namespace Guard.Core.Services.Exports;

// Validates the serialized contract without building a query or referencing UI descriptors.
public static class ExportFilterValidation
{
  private static readonly HashSet<string> Operations = ["Equals", "NotEquals", "LessThan", "LessThanOrEquals",
    "GreaterThan", "GreaterThanOrEquals", "Contains", "StartsWith", "EndsWith", "DoesNotContain",
    "In", "NotIn", "IsNull", "IsNotNull", "IsEmpty", "IsNotEmpty"];

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
      if (!fields.TryGetValue(node.Field, out var type) || node.Operation == null || !Operations.Contains(node.Operation))
        throw new ArgumentException("Недопустимое поле или операция фильтра.");
      if (type != typeof(string) && node.Operation is "Contains" or "StartsWith" or "EndsWith" or "DoesNotContain" or "In" or "NotIn" or "IsEmpty" or "IsNotEmpty")
        throw new ArgumentException("Недопустимая операция для типа поля.");
      _ = ReadValue(node.Value, type);
    }
    if (filter != null) Visit(filter, 0);
  }

  internal static object? ReadValue(JsonElement? value, Type type)
  {
    if (value is null or { ValueKind: JsonValueKind.Null }) return null;
    try {
      if (value.Value.ValueKind == JsonValueKind.Array) {
        if (type != typeof(string)) throw new ArgumentException("Недопустимое значение фильтра.");
        return value.Value.Deserialize<string[]>();
      }
      var result = value.Value.Deserialize(type);
      return result is DateTimeOffset offset ? offset.ToUniversalTime() : result;
    }
    catch (JsonException ex) { throw new ArgumentException("Недопустимое значение фильтра.", ex); }
  }
}
