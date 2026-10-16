using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

public interface IPersonalEditorService
{
  Task<PersonalEditorDto?> GetEditorAsync(Guid id, CancellationToken ct = default);
  Task<IReadOnlyList<PersonalSubdivisionOptionDto>> GetSubdivisionOptionsAsync(CancellationToken ct = default);
  Task<Guid> CreateFromDtoAsync(CreatePersonalDto input, CancellationToken ct = default);
  Task UpdateFromDtoAsync(EditPersonalDto input, CancellationToken ct = default);
}
