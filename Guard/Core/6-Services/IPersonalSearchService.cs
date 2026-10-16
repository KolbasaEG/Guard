using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

public interface IPersonalSearchService
{
  Task<PersonalSearchResult> SearchAsync(PersonalSearchRequest request, CancellationToken ct = default);
}
