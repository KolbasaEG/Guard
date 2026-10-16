namespace Guard.Core.Services.DTOs;

public enum OwnPasswordState { MissingUser, NoPassword, Ready }

public enum OwnPasswordChangeStatus { Succeeded, Unauthorized, Rejected }

public sealed record OwnPasswordChangeResult(OwnPasswordChangeStatus Status, string? Error = null);
