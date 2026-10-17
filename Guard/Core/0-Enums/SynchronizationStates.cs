namespace Guard.Core.Entities;

public enum DataManagementMode { Local = 0, Api = 1 }
public enum SynchronizationStatus { Running = 0, Succeeded = 1, Failed = 2, Cancelled = 3, Interrupted = 4, Queued = 5 }
