using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

public interface IPersonalDetailsService
{
  Task<PersonalDetailsDto?> GetDetailsAsync(Guid id, CancellationToken ct = default);
}
