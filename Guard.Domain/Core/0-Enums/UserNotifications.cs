namespace Guard.Core.Enums;

public enum UserNotificationKind { ExportReady = 1, ExportFailed = 2, SynchronizationResult = 3, ScheduledSynchronization = 4, TechnicalLogging = 5 }
public enum UserNotificationSeverity { Information = 1, Success = 2, Warning = 3, Error = 4 }
public enum NotificationTarget { Export = 1, Synchronization = 2, TechnicalLogs = 3 }
