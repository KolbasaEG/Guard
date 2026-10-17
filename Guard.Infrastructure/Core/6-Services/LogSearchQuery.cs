using System.Collections.Frozen;
using Guard.Core.Entities;
using Guard.Core.Services.Exports;

namespace Guard.Core.Services;

public static class LogSearchQuery
{
  private static readonly string[] columns = ["Timestamp", "Level", "Layer", "UserId", "ClientIp", "Message", "Exception"];
  public static readonly FrozenSet<string> Fields = LogSearchFilter.Fields;
  public static void ValidateFilter(ExportFilter? filter) => LogSearchFilter.Validate(filter);
  public static IQueryable<LogEntry> Filter(IQueryable<LogEntry> query, ExportFilter? filter) => RestrictedSearchQuery<LogEntry>.Filter(query, filter, Fields);
  // The existing table is keyless. All visible values resolve ties between distinct rows.
  public static IOrderedQueryable<LogEntry> Sort(IQueryable<LogEntry> query, string? orderBy) => RestrictedSearchQuery<LogEntry>.Sort(query, orderBy, Fields, "Timestamp desc", columns);
}
