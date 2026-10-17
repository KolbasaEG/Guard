namespace Guard.Core.Services.DTOs;

/// <summary>Снимок для отображения. Не является основанием серверного доступа.</summary>
public sealed record CurrentUserDisplayDto(
    string UserId, string UserName, bool IsRoot, string? PersonalFullName,
    string? SubdivisionName, IReadOnlyList<CurrentUserSubdivisionDto> SubordinateSubdivisions);

public sealed record CurrentUserSubdivisionDto(Guid Id, string Name);
