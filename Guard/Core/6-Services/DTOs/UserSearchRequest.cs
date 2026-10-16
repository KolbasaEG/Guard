using Guard.Core.Services.Exports;

namespace Guard.Core.Services.DTOs;

public sealed record UserSearchRequest(int Skip = 0, int Take = 20, string? OrderBy = null, ExportFilter? Filter = null);
public sealed record UserSearchResult(IReadOnlyList<UserDto> Items, int Count);
