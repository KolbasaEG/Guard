using Guard.Core.Enums;

namespace Guard.Core.Services;

/// <summary>
/// Реализация сервиса аудита Identity событий.
/// Отправляет данные в RuSIEM по TcpSyslog + TLS в формате CEF.
/// </summary>
public sealed class AuditService(Serilog.ILogger auditLogger) : IAuditService
{
  public void LogIdentityEvent(
      AuditEventType eventType,
      string? userName,
      string? ipAddress = null,
      string? details = null)
  {
    var cefMessage = BuildCefMessage(eventType, userName, ipAddress, details);

    auditLogger
        .ForContext("EventType", eventType.ToString())
        .ForContext("UserName", userName ?? "Anonymous")
        .ForContext("SourceIp", ipAddress)
        .Information(cefMessage);
  }

  /// <summary>
  /// Формирует сообщение в формате CEF (рекомендуется RuSIEM).
  /// </summary>
  private static string BuildCefMessage(
      AuditEventType eventType,
      string? userName,
      string? ipAddress,
      string? details)
  {
    var severity = eventType switch
    {
      AuditEventType.LoginFailed => 8,
      AuditEventType.PasswordChanged => 7,
      AuditEventType.RoleAssigned or AuditEventType.RoleRemoved => 7,
      _ => 5
    };

    var outcome = eventType.ToString().Contains("Failed") ? "failure" : "success";

    return $"CEF:0|Guard|IdentityAudit|1.0|{eventType}|{eventType}|{severity}|" +
           $"src={ipAddress ?? "-"} " +
           $"user={userName ?? "unknown"} " +
           $"outcome={outcome} " +
           $"msg={details ?? eventType.ToString()}";
  }
}