using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

// Порт чтения для допуска до создания текущей сессии. Не вычисляет permissions.
public interface IIpAccessReader
{
  Task<IpAccessSubject?> FindSubjectAsync(string userId, CancellationToken ct = default);
  Task<IReadOnlyList<string>> GetActiveRulesAsync(Guid personalId, CancellationToken ct = default);
}
