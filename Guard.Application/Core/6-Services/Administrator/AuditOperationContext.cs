namespace Guard.Core.Services;

public sealed record AuditOperationMetadata(string? UserId, string? ClientIp, string CorrelationId, string ActorKind);

/// <summary>Контекст одной HTTP/circuit-операции, а не состояние singleton или всего circuit.</summary>
public static class AuditOperationContext
{
  private static readonly AsyncLocal<AuditOperationMetadata?> CurrentSlot = new();
  public static AuditOperationMetadata? Current => CurrentSlot.Value;
  public static IDisposable Push(AuditOperationMetadata value)
  {
    var previous = CurrentSlot.Value; CurrentSlot.Value = value; return new Scope(previous);
  }
  private sealed class Scope(AuditOperationMetadata? previous) : IDisposable
  {
    public void Dispose() => CurrentSlot.Value = previous;
  }
}
