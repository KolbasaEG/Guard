namespace Guard.Core.Services.DTOs;

public enum ApplicationLogLevel
{
  Verbose,
  Debug,
  Information,
  Warning,
  Error,
  Fatal
}

public sealed record LogConfigurationDto(int MaxSessions, ApplicationLogLevel MinimumLevel);
