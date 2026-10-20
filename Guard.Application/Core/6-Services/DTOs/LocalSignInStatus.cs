namespace Guard.Core.Services.DTOs;

public enum LocalSignInStatus
{
  Succeeded = 0,
  Failed,
  NotAllowed,
  LockedOut,
  RequiresTwoFactor,
  RecoveryOnly = 100,
  AuditUnavailable = 101
}
