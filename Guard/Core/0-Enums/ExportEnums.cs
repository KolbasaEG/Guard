namespace Guard.Core.Enums;

public enum ExportArea { Personals = 1, Subdivisions = 2, IpAddresses = 3 }
public enum ExportFormat { Excel = 1, Word = 2 }
public enum ExportJobState { Queued = 1, Processing = 2, Ready = 3, Delivering = 4, Delivered = 5, Failed = 6, Cancelled = 7, Expired = 8 }
