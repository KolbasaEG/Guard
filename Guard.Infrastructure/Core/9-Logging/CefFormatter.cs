using Guard.Core.Enums;
using Guard.Core.Services;

namespace Guard.Core.Logging;

public static class CefFormatter
{
  public static string Format(AuditEventData e) => Format(e.EventType, e.Actor, e.ClientIp, e.Details, e.Outcome) +
    $" rt={new DateTimeOffset(e.OccurredAtUtc).ToUnixTimeMilliseconds()}" +
    $" externalId={e.EventId:D} cs1Label=CorrelationId cs1={Escape(e.CorrelationId ?? "-")}" +
    $" cs2Label=ObjectType cs2={Escape(e.ObjectType ?? "-")} cs3Label=ObjectId cs3={Escape(e.ObjectId ?? "-")}" +
    $" cs4Label=ActorKind cs4={Escape(e.ActorKind)} cs5Label=ReasonCode cs5={Escape(e.ReasonCode ?? "-")}" +
    $" cs6Label=AttemptedAccount cs6={Escape(e.AttemptedAccount ?? "-")}";
  public static string Format(AuditEventType eventType, string? userName, string? ipAddress, string? details, string? recordedOutcome = null)
  {
    var severity = eventType switch {
      AuditEventType.LoginFailed or AuditEventType.AccessDenied => 8,
      AuditEventType.PasswordChanged or AuditEventType.RoleAssigned or AuditEventType.RoleRemoved or
        AuditEventType.RolePermissionsChanged or AuditEventType.UserRolesChanged => 7,
      _ => 5
    };
    var outcome = eventType == AuditEventType.AccessDenied || eventType.ToString().Contains("Failed", StringComparison.Ordinal)
      ? "failure" : eventType == AuditEventType.ExportCancelled ? "cancelled" : "success";
    if (recordedOutcome is "success" or "failure" or "cancelled" or "unknown" or "started" or "denied") outcome = recordedOutcome;
    return $"CEF:0|Guard|IdentityAudit|1.0|{eventType}|{eventType}|{severity}|" +
      $"src={Escape(ipAddress ?? "-")} suser={Escape(userName ?? "unknown")} outcome={outcome} msg={Escape(details ?? eventType.ToString())}";
  }

  public static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
    .Replace("=", "\\=", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal)
    .Replace("\n", "\\n", StringComparison.Ordinal).Replace("\0", "\\0", StringComparison.Ordinal);
}
