namespace Guard.Core.Services.DTOs;

public record IpOptionDto(Guid Id, string Address);
public record PersonalIpAssignmentsDto(IReadOnlyList<IpOptionDto> Available, IReadOnlyList<Guid> Selected, int HiddenAssignmentCount = 0);
