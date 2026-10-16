using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

/// <summary>Операции UI без query-делегатов и моделей хранения.</summary>
public interface IIpAddressManagementService
{
  Task<IpAddressDetailsDto?> GetDetailsAsync(Guid id, CancellationToken ct = default);
  Task<IReadOnlyList<IpSubdivisionOptionDto>> GetSubdivisionOptionsAsync(CancellationToken ct = default);
  Task<Guid> CreateAsync(CreateIpAddressDto input, CancellationToken ct = default);
  Task UpdateAsync(EditIpAddressDto input, CancellationToken ct = default);
  Task<IpSearchResult> SearchAsync(IpSearchRequest request, CancellationToken ct = default);
  Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
  Task ArchiveAsync(Guid id, CancellationToken ct = default);
  Task BlockAsync(Guid id, CancellationToken ct = default);
  Task UnblockAsync(Guid id, CancellationToken ct = default);
  Task RestoreAsync(Guid id, CancellationToken ct = default);
}
