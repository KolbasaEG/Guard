using Guard.Core.Enums;

namespace Guard.Core.Services;

/// <summary>
/// Интерфейс сервиса аудита безопасности.
/// Все вызовы аудита Identity должны идти через этот интерфейс.
/// </summary>
public interface IAuditService
{
  void Record(AuditEventData entry) => RecordAsync(entry).GetAwaiter().GetResult();

  /// <summary>Возврат подтверждает обязательное локальное сохранение; ошибка не подавляется.</summary>
  Task RecordAsync(AuditEventData entry, CancellationToken ct = default);

  /// <summary>
  /// Совместимый адаптер события аккаунта: обязательное локальное сохранение,
  /// а в режиме SIEM — постановка на последующую отправку Syslog/TLS/CEF.
  /// </summary>
  void LogIdentityEvent(
      AuditEventType eventType,
      string? userName,
      string? ipAddress = null,
      string? details = null) => Record(AuditEventData.ForAccount(eventType, userName, ipAddress, details));
}
