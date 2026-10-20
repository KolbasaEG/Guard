namespace Guard.Core.Services;

/// <summary>Серверная идентичность сеанса. Не является подтверждением полномочий.</summary>
public sealed record CurrentSession(string UserId, string? SecurityStamp, bool AuditRecoveryOnly = false);

public interface ICurrentSession
{
  Task<CurrentSession?> GetAsync(CancellationToken ct = default);
}
