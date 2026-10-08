using Serilog.Events;
namespace Guard.Core.Services;
public interface ILogConfigurationService
{
  Task SaveAsync(int maxSessions, LogEventLevel level, CancellationToken ct = default);
}
