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
  RoleRemoved = 13,
  RolePermissionsChanged = 14,
  UserRolesChanged = 15,
  AccessDenied = 16,
  UserBlocked = 17,
  UserUnblocked = 18,
  AccountPolicyChanged = 19,
  SynchronizationSettingsChanged = 20,
  SynchronizationCompleted = 21,
  ExportRequested = 22,
  ExportArchived = 23,
  ExportDeliveryStarted = 24,
  ExportDeliveryCompleted = 25,
  ExportFailed = 26,
  ExportCancelled = 27,
  ExportArchiveViewed = 28,
  ExportRetentionChanged = 29,
  ExportExpired = 30,
  UserDetailsChanged = 31,
  UserCreated = 32
}
