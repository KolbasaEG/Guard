using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

/// <summary>Операции UI без query-делегатов и моделей хранения.</summary>
public interface ISubdivisionManagementService
{
  Task<SubdivisionDetailsDto?> GetDetailsAsync(Guid id, CancellationToken ct = default);
  Task<SubdivisionEditorOptionsDto> GetEditorOptionsAsync(CancellationToken ct = default);
  Task<IReadOnlyList<SubdivisionParentOptionDto>> GetMoveParentOptionsAsync(Guid id, CancellationToken ct = default);
  Task<SubdivisionSearchResult> SearchAsync(SubdivisionSearchRequest request, CancellationToken ct = default);
  Task MoveAsync(Guid id, Guid? newParentId, CancellationToken ct = default, Guid? expectedVersion = null);
  Task SoftDeleteAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
  Task ArchiveAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
  Task BlockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
  Task UnblockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
  Task RestoreAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
  Task RebuildHierarchyAndPathsAsync(CancellationToken ct = default);
  Task<Guid> CreateFromDtoAsync(CreateSubdivisionDto input, CancellationToken ct = default);
  Task UpdateFromDtoAsync(EditSubdivisionDto input, CancellationToken ct = default);
}
