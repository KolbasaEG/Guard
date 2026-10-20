using Guard.Core.Enums;

namespace Guard.Core.Services;

public sealed record AuditEventData(Guid EventId, DateTime OccurredAtUtc, AuditEventType EventType,
  string? Actor, string ActorKind, string Outcome, string? ClientIp = null, string? CorrelationId = null,
  string? ObjectType = null, string? ObjectId = null, string? Details = null,
  string? ReasonCode = null, string? AttemptedAccount = null)
{
  public AuditEventData Normalize() => this with {
    Actor = Limit(Actor, 256), ActorKind = Limit(ActorKind, 16)!, Outcome = Limit(Outcome, 16)!,
    ClientIp = Limit(ClientIp, 64), CorrelationId = Limit(CorrelationId, 128), ObjectType = Limit(ObjectType, 64),
    ObjectId = Limit(ObjectId, 128), Details = Limit(Details, 16384),
    ReasonCode = Limit(ReasonCode, 128), AttemptedAccount = Limit(AttemptedAccount, 256)
  };

  private static string? Limit(string? value, int maximum)
  {
    value = value?.Replace("\0", "\\0", StringComparison.Ordinal);
    if (value == null || value.Length <= maximum) return value;
    var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)))[..12];
    var suffix = $"...[{digest}]";
    if (maximum <= suffix.Length) return value[..maximum];
    var length = maximum - suffix.Length;
    if (char.IsHighSurrogate(value[length - 1])) length--;
    return value[..length] + suffix;
  }

  public static AuditEventData Create(AuditEventType type, string? actor, string? ip = null, string? details = null) =>
    new(Guid.NewGuid(), DateTime.UtcNow, type, actor ?? AuditOperationContext.Current?.UserId,
      actor != null ? "user" : AuditOperationContext.Current?.ActorKind ?? "system",
      type == AuditEventType.AccessDenied || type.ToString().Contains("Failed", StringComparison.Ordinal) ? "failure"
        : type == AuditEventType.ExportCancelled ? "cancelled" : "success", ip ?? AuditOperationContext.Current?.ClientIp,
      AuditOperationContext.Current?.CorrelationId ?? System.Diagnostics.Activity.Current?.TraceId.ToString(), Details: details);

  public static AuditEventData ForAccount(AuditEventType type, string? account, string? ip = null, string? details = null) =>
    Create(type, AuditOperationContext.Current?.UserId, ip, details) with {
      ActorKind = AuditOperationContext.Current?.ActorKind ?? "system", ObjectType = "Account", ObjectId = account
    };
}
