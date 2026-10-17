using Serilog.Core;
using Serilog.Events;

namespace Guard.Core.Logging;

public class LayerEnricher : ILogEventEnricher
{
  public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
  {
    if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContextValue) &&
        sourceContextValue is ScalarValue { Value: string sourceContext })
    {
      string layer = sourceContext switch
      {
        var s when s.StartsWith("Guard.Components") => "Components",
        var s when s.StartsWith("Guard.Core.Services") => "Service",
        var s when s.StartsWith("Microsoft.EntityFrameworkCore") => "Database",
        _ => "System"
      };

      logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Layer", layer));
    }
    else
    {
      logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("Layer", "System"));
    }
  }
}