using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Guard.Core.Services;

public sealed class AuditAdministrationService(ICurrentSession session, IPermissionReader reader,
  IDbContextFactory<ApplicationDbContext> factory, AuditJournal journal, AuditHealth health) : IAuditAdministrationService
{
  private async Task<string> RootAsync(CancellationToken ct)
  {
    var current = await session.GetAsync(ct) ?? throw new UnauthorizedAccessException();
    var stamp = await reader.GetSecurityStampAsync(current.UserId, ct);
    var access = await reader.FindAccessAsync(current.UserId, ct);
    if (string.IsNullOrEmpty(stamp) || stamp != current.SecurityStamp || access?.RoleNames.Contains("ROOT") != true)
      throw new UnauthorizedAccessException("Диагностика аудита доступна только действительному Root.");
    await using var db = await factory.CreateDbContextAsync(ct);
    var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == current.UserId, ct);
    if (user == null || user.AccountBlockReason != null || user.LockoutEnabled && user.LockoutEnd > DateTimeOffset.UtcNow)
      throw new UnauthorizedAccessException("Учётная запись недоступна.");
    return current.UserId;
  }
  public async Task RequireRootAsync(CancellationToken ct = default) => _ = await RootAsync(ct);
  public async Task<AuditConfiguration> GetConfigurationAsync(CancellationToken ct = default)
  {
    await RootAsync(ct); var result = await journal.GetConfigurationAsync(ct); await RootAsync(ct); return result;
  }
  public async Task<AuditSystemState> GetStateAsync(CancellationToken ct = default)
  {
    await RootAsync(ct); var result = await journal.GetStateAsync(ct); await RootAsync(ct); return result;
  }
  public async Task SaveAsync(AuditConfiguration configuration, bool transferPending, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(configuration);
    var copy = JsonSerializer.Deserialize<AuditConfiguration>(JsonSerializer.Serialize(configuration))!;
    Validate(copy);
    var actor = await RootAsync(ct);
    await journal.SaveConfigurationAsync(copy, actor, transferPending, RootAsync, ct);
  }
  public async Task<AuditConnectionTest> TestConnectionAsync(AuditConfiguration configuration, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(configuration);
    var copy = JsonSerializer.Deserialize<AuditConfiguration>(JsonSerializer.Serialize(configuration))!;
    Validate(copy, true); var actor = await RootAsync(ct);
    var entry = AuditEventData.Create(AuditEventType.AuditConnectionTest, actor, details: "Проверка соединения Root") with {
      Outcome = "started", ObjectType = "SIEM", ObjectId = copy.Host + ":" + copy.Port
    };
    await journal.WriteAsync(entry, ct);
    try {
      var transport = new SyslogAuditTransport(Options.Create(new AuditSyslogOptions {
        Host = copy.Host, Port = copy.Port, CaCertificatePem = copy.CaCertificatePem
      }), null, health);
      await transport.SendAsync(new AuditEventRecord { EventId = entry.EventId, EventType = entry.EventType,
        OccurredAtUtc = entry.OccurredAtUtc, Actor = actor, ActorKind = "user", Outcome = "started", Details = entry.Details,
        CorrelationId = entry.CorrelationId, ObjectType = entry.ObjectType, ObjectId = entry.ObjectId, ClientIp = entry.ClientIp }, ct);
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
    catch (Exception error) {
      await journal.WriteAsync(entry with { EventId = Guid.NewGuid(), Outcome = "failure", ReasonCode = error.GetType().Name }, ct);
      return new(false, "Проверка TLS или отправка не выполнена: " + error.GetType().Name, DateTime.UtcNow, null);
    }
    try { await journal.WriteAsync(entry with { EventId = Guid.NewGuid(), Outcome = "success", ReasonCode = "TlsWriteCompleted" }, ct); }
    catch (Exception error) { throw new OperationCommitUnknownException(entry.CorrelationId, error); }
    return new(true, "TLS-соединение и тестовая отправка выполнены. Проверьте EventId " + entry.EventId + " в RuSIEM.", DateTime.UtcNow, health.CertificateExpiresAtUtc);
  }
  internal static void Validate(AuditConfiguration c, bool test = false)
  {
    c.Host = c.Host?.Trim() ?? ""; c.CaCertificatePem = string.IsNullOrWhiteSpace(c.CaCertificatePem) ? null : c.CaCertificatePem.Trim();
    if (!Enum.IsDefined(c.Mode) || c.RetentionYears is < 1 or > 30 || c.MaxQueueCount < 0 ||
        c.MaxQueueAgeMinutes < 0 || c.MaxQueueAgeMinutes > 525600 || c.MaxQueueBytes < 0)
      throw new ArgumentException("Проверьте режим, срок хранения (1–30 лет) и ограничения очереди.");
    if (test || c.Mode == AuditMode.Siem) {
      if (c.Host.Length is < 1 or > 253 || Uri.CheckHostName(c.Host) == UriHostNameType.Unknown || c.Port is < 1 or > 65535)
        throw new ArgumentException("Укажите корректное имя или IP сервера RuSIEM и порт 1–65535.");
      if (!test && (c.MaxQueueAgeMinutes < 1 || c.MaxQueueBytes < 1024))
        throw new ArgumentException("Для SIEM задайте максимальный возраст от 1 минуты и объём очереди от 1024 байт.");
    }
    if (c.CaCertificatePem != null) {
      if (c.CaCertificatePem.Length > 16384 || c.CaCertificatePem.Contains("PRIVATE KEY", StringComparison.Ordinal))
        throw new ArgumentException("Допустим только публичный PEM-сертификат CA до 16 КиБ.");
      try { using var certificate = X509Certificate2.CreateFromPem(c.CaCertificatePem); }
      catch (System.Security.Cryptography.CryptographicException) { throw new ArgumentException("Не удалось прочитать публичный сертификат CA."); }
    }
  }
}
