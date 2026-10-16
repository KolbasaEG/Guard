using Guard.Core.Services.DTOs;
namespace Guard.Core.Services;
public interface ILogConfigurationService
{
  Task<LogConfigurationDto> GetAsync(CancellationToken ct = default);
  Task<LogConfigurationDto> SaveAsync(LogConfigurationDto settings, CancellationToken ct = default);
}
