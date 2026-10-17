using NpgsqlTypes;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;

namespace Guard.Core.Logging;

// The existing logs.timestamp column is TIMESTAMP without time zone.
// Store UTC wall-clock values; an Unspecified Kind is required by Npgsql for this column type.
public sealed class UtcTimestampColumnWriter() : ColumnWriterBase(NpgsqlDbType.Timestamp)
{
  public override object GetValue(LogEvent logEvent, IFormatProvider? formatProvider = null) =>
    DateTime.SpecifyKind(logEvent.Timestamp.UtcDateTime, DateTimeKind.Unspecified);
}
