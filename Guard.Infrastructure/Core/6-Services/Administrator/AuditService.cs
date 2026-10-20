using Guard.Core.Enums;
using Guard.Core.Logging;

namespace Guard.Core.Services;

/// <summary>Независимый от уровня технических логов канал аудита.</summary>
public sealed class AuditService(Serilog.ILogger auditLogger, AuditJournal? journal = null) : IAuditService, IDisposable
{
  internal AuditJournal? Journal => journal;
  public void Dispose() => (auditLogger as IDisposable)?.Dispose();

  public void LogIdentityEvent(AuditEventType eventType, string? userName, string? ipAddress = null, string? details = null)
  {
    var entry = AuditEventData.ForAccount(eventType, userName, ipAddress, details);
    if (eventType is AuditEventType.LoginFailed or AuditEventType.LoginSuccess)
      entry = entry with { ObjectId = null, AttemptedAccount = userName };
    Record(entry);
  }

  public void Record(AuditEventData entry)
  {
    entry = entry.Normalize();
    if (journal == null) throw new AuditUnavailableException("NotConfigured", "Обязательный журнал аудита не зарегистрирован.");
    journal.Write(entry);
    auditLogger.ForContext("EventType", entry.EventType.ToString())
      .ForContext("EventId", entry.EventId).ForContext("UserName", entry.Actor ?? "Anonymous").ForContext("SourceIp", entry.ClientIp)
      .Information("{CefMessage:l}", CefFormatter.Format(entry));
  }

  public async Task RecordAsync(AuditEventData entry, CancellationToken ct = default)
  {
    entry = entry.Normalize();
    if (journal == null) throw new AuditUnavailableException("NotConfigured", "Обязательный журнал аудита не зарегистрирован.");
    await journal.WriteAsync(entry, ct);
    auditLogger.ForContext("EventId", entry.EventId).ForContext("EventType", entry.EventType.ToString())
      .Information("{CefMessage:l}", CefFormatter.Format(entry));
  }
}
