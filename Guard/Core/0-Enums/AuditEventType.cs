namespace Guard.Core.Enums;

/// <summary>
/// Типы событий аудита Identity (для отправки в RuSIEM)
/// </summary>
public enum AuditEventType
{
  LoginSuccess = 1,
  LoginFailed = 2,
  Logout = 3,
  PasswordChanged = 4,
  PasswordResetRequested = 5,
  TwoFactorEnabled = 6,
  TwoFactorDisabled = 7,
  PasskeyRegistered = 8,
  PasskeyRemoved = 9,
  ExternalLoginAdded = 10,
  ExternalLoginRemoved = 11,
  RoleAssigned = 12,
  RoleRemoved = 13
}