using Serilog.Events;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Guard.Core.Logging;

public sealed record TechnicalLogRecord(Guid Id, DateTime Timestamp, string Level, string Layer, string Message,
  string MessageTemplate, string? Exception, string Properties, string? UserId, string? ClientIp,
  string? CorrelationId, string? SourceContext, Guid? DiagnosticSessionId, string? RunId, string Fingerprint,
  string ApplicationVersion, string InstanceId)
{
  public static TechnicalLogRecord Create(LogEvent entry, TechnicalLogRuntime runtime)
  {
    var properties = entry.Properties.Take(48).ToDictionary(p => p.Key, p => TechnicalLogPrivacy.Value(p.Key, p.Value));
    string? Get(string name) => properties.TryGetValue(name, out var value) ? value as string : null;
    var category = Get("SourceContext");
    var session = runtime.SessionFor(category);
    var template = TechnicalLogPrivacy.Text(entry.MessageTemplate.Text, 2048);
    var sanitized = new LogEvent(entry.Timestamp, entry.Level, null,
      new Serilog.Parsing.MessageTemplateParser().Parse(template),
      properties.Select(p => new LogEventProperty(p.Key, new ScalarValue(p.Value))));
    var exception = entry.Exception == null ? null : TechnicalException.Describe(entry.Exception);
    var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
      category + "\n" + template + "\n" + entry.Exception?.GetType().FullName)));
    properties["EventCode"] = Get("EventCode") ??
      (entry.Properties.TryGetValue("EventId", out var eventId) && eventId is StructureValue eventStructure
        ? string.Join(':', eventStructure.Properties.Where(p => p.Name is "Id" or "Name").Select(p => TechnicalLogPrivacy.Text(p.Value.ToString(), 128)))
        : fingerprint[..16]);
    if (entry.Exception is { } failure) {
      properties["ErrorType"] = failure.GetType().FullName;
      if (TechnicalException.FirstCode(failure) is { } code) properties["ErrorCode"] = code;
    }
    return new(Guid.NewGuid(), entry.Timestamp.UtcDateTime, entry.Level.ToString(), Limit(Get("Layer"), 50) ?? "System",
      TechnicalLogPrivacy.Text(sanitized.RenderMessage(System.Globalization.CultureInfo.InvariantCulture), 8192), template,
      exception, System.Text.Json.JsonSerializer.Serialize(properties), Limit(Get("UserId"), 250), Limit(Get("ClientIp"), 50),
      Get("CorrelationId") ?? Guard.Core.Services.AuditOperationContext.Current?.CorrelationId ?? System.Diagnostics.Activity.Current?.TraceId.ToString(), category, session?.Id,
      Get("DiagnosticRunId") ?? Get("RunId"), fingerprint,
      typeof(TechnicalLogRecord).Assembly.GetName().Version?.ToString() ?? "unknown", runtime.InstanceId);
  }
  private static string? Limit(string? value, int maximum) => value?.Length > maximum ? value[..maximum] : value;
}

/// <summary>Общая политика новых событий и экспорта: ограничение размера и отказ от произвольных строковых payload.</summary>
public static partial class TechnicalLogPrivacy
{
  public static Dictionary<string, object?> ExportProperties(string? json)
  {
    var result = new Dictionary<string, object?>();
    if (json == null || json.Length > 65536) return result;
    try {
      using var document = System.Text.Json.JsonDocument.Parse(json, new() { MaxDepth = 16 });
      if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return result;
      foreach (var property in document.RootElement.EnumerateObject().Take(48)) {
        if (SensitiveName().IsMatch(property.Name)) continue;
        if (property.Value.ValueKind == System.Text.Json.JsonValueKind.String && strings.Contains(property.Name))
          result[Text(property.Name, 128)] = Value(property.Name, new ScalarValue(property.Value.GetString()));
        else if (property.Value.ValueKind == System.Text.Json.JsonValueKind.Number && property.Value.TryGetDecimal(out var number)) result[Text(property.Name, 128)] = number;
        else if (property.Value.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False) result[Text(property.Name, 128)] = property.Value.GetBoolean();
      }
    } catch (System.Text.Json.JsonException) { }
    return result;
  }
  private static readonly HashSet<string> strings = new(StringComparer.OrdinalIgnoreCase) {
    "SourceContext", "Layer", "UserId", "ClientIp", "CorrelationId", "RunId", "DiagnosticRunId", "EventId",
    "Operation", "Stage", "Result", "Outcome", "ReasonCode", "ErrorCode", "ErrorType", "EntityType", "EntityId",
    "RequestMethod", "RequestPath", "Protocol", "ConnectionId", "RequestId", "TraceId", "SpanId", "JobId",
    "BatchId", "ExportId", "Mode", "State", "Status", "Level", "Method", "Category", "Action", "EventCode",
    "ApiResource", "DiagnosticIssueCode", "DiagnosticPhase", "DiagnosticExceptionType", "SyncPhase", "SyncStatus", "ManagementMode"
  };
  public static object? Value(string key, LogEventPropertyValue value)
  {
    if (SensitiveName().IsMatch(key)) return "[скрыто]";
    if (value is ScalarValue scalar) {
      if (scalar.Value is null or bool or byte or short or int or long or float or double or decimal) return scalar.Value;
      if (scalar.Value is Guid or DateTime or DateTimeOffset or TimeSpan or Enum) return scalar.Value.ToString();
      if (strings.Contains(key)) {
        var text = scalar.Value?.ToString() ?? "";
        if (key is "RequestPath") text = text.Split('?')[0];
        return Text(text, 1024);
      }
    }
    // Неструктурированные внешние ответы, коллекции и неизвестные строки могут содержать секреты и ПДн.
    return "[скрыто]";
  }
  public static string Text(string value, int limit = 8192)
  {
    var bounded = (value.Length > limit ? value[..limit] + "…[обрезано]" : value).Replace('\0', '\uFFFD');
    bounded = SecretAssignment().Replace(bounded, "$1=[скрыто]");
    bounded = Bearer().Replace(bounded, "Bearer [скрыто]");
    return Email().Replace(bounded, "[адрес скрыт]");
  }
  [GeneratedRegex("password|passwd|secret|token|authorization|cookie|credential|connectionstring|api.?key|payload|body|response|email|phone", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
  private static partial Regex SensitiveName();
  [GeneratedRegex("(password|passwd|pwd|secret|token|api[_-]?key|authorization|cookie)\\s*[:=]\\s*[^\\s;,]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
  private static partial Regex SecretAssignment();
  [GeneratedRegex("Bearer\\s+[A-Za-z0-9._~+/-]+=*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
  private static partial Regex Bearer();
  [GeneratedRegex("[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
  private static partial Regex Email();
}
