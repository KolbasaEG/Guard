using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

public interface IPersonalIpService
{
  Task<PersonalIpAssignmentsDto> GetAsync(Guid personalId, CancellationToken ct = default);
  Task UpdateAsync(Guid personalId, IEnumerable<Guid> ipIds, CancellationToken ct = default);
}
