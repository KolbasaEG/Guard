using Guard.Core.Enums;

namespace Guard.Core.Services.DTOs;

public sealed record IpAccessSubject(bool IsRoot, Guid? PersonalId, Status? PersonalStatus);
