namespace Guard.Core.Services.DTOs;

public sealed record PermissionAccessData(IReadOnlyList<string?> RoleNames, IReadOnlyList<string?> PermissionCodes);
