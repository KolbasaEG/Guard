namespace Guard.Core.Services.DTOs;

public enum LocalSignInStatus
{
  Succeeded,
  Failed,
  NotAllowed,
  LockedOut,
  RequiresTwoFactor
}
