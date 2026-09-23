using Guard.Core.Enums;

namespace Guard.Core.Services;

/// <summary>
/// Интерфейс сервиса аудита безопасности.
/// Все вызовы аудита Identity должны идти через этот интерфейс.
/// </summary>
public interface IAuditService
{
  /// <summary>
  /// Логирование события Identity в RuSIEM (через TcpSyslog + TLS + CEF).
  /// </summary>
  void LogIdentityEvent(
      AuditEventType eventType,
      string? userName,
      string? ipAddress = null,
      string? details = null);
}