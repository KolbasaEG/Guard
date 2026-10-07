using Guard.Core.Enums;
namespace Guard.Core.Services.DTOs;
public record PersonalSearchRequest(string? Search = null, Guid? SubdivisionId = null,
    bool IncludeChildren = true, DataViewMode Mode = DataViewMode.Active,
    int Skip = 0, int Take = 20, string? OrderBy = null);
public record PersonalListItemDto(Guid Id, string LastName, string FirstName, string? MiddleName,
    string? FullName, string? SubdivisionName, Status Status);
public record PersonalSearchResult(IReadOnlyList<PersonalListItemDto> Items, int Count);
