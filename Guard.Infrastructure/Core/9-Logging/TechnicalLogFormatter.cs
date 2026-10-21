using Serilog.Events;
using Serilog.Formatting;
using System.Text.Json;

namespace Guard.Core.Logging;

/// <summary>Bootstrap использует ту же очистку, что основной технический канал, включая безопасный граф исключений.</summary>
public sealed class TechnicalLogFormatter(TechnicalLogRuntime runtime) : ITextFormatter
{
  public void Format(LogEvent entry, TextWriter output)
  {
    output.Write(JsonSerializer.Serialize(TechnicalLogRecord.Create(entry, runtime)));
    output.WriteLine();
  }
}
