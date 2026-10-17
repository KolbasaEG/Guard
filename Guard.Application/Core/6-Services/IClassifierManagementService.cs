using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

/// <summary>Операции UI без query-делегатов и моделей хранения.</summary>
public interface IClassifierManagementService
{
  Task<ClassifierSearchResult> SearchAsync(ClassifierSearchRequest request, CancellationToken ct = default);
  Task<ClassifierDetailsDto?> GetDetailsAsync(int id, CancellationToken ct = default);
  Task SetActiveStatusAsync(int id, bool isActive, CancellationToken ct = default, Guid? expectedVersion = null);
  Task DeleteAsync(int id, CancellationToken ct = default, Guid? expectedVersion = null);
  Task<int> CreateFromDtoAsync(CreateClassifierDto input, CancellationToken ct = default);
  Task UpdateFromDtoAsync(EditClassifierDto input, CancellationToken ct = default);
}
