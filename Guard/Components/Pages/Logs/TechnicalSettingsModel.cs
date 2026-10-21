using Guard.Core.Services.DTOs;

namespace Guard.Components.Pages.Logs;

public sealed class TechnicalSettingsModel
{
  public ApplicationLogLevel NormalLevel { get; set; } = ApplicationLogLevel.Information;
  public int RetentionDays { get; set; }
  public int BufferMegabytes { get; set; }
  public int MinimumFreeMegabytes { get; set; }
  public int DefaultDiagnosticMinutes { get; set; }
  public int MaximumDiagnosticMinutes { get; set; }
  public int ErrorThreshold { get; set; }
  public int ErrorWindowMinutes { get; set; }
  public TechnicalLogSettings ToSettings() => new() { NormalLevel = NormalLevel, RetentionDays = RetentionDays,
    BufferMegabytes = BufferMegabytes, MinimumFreeMegabytes = MinimumFreeMegabytes,
    DefaultDiagnosticMinutes = DefaultDiagnosticMinutes, MaximumDiagnosticMinutes = MaximumDiagnosticMinutes,
    ErrorThreshold = ErrorThreshold, ErrorWindowMinutes = ErrorWindowMinutes };
  public static TechnicalSettingsModel From(TechnicalLogSettings value) => new() { NormalLevel = value.NormalLevel,
    RetentionDays = value.RetentionDays, BufferMegabytes = value.BufferMegabytes, MinimumFreeMegabytes = value.MinimumFreeMegabytes,
    DefaultDiagnosticMinutes = value.DefaultDiagnosticMinutes, MaximumDiagnosticMinutes = value.MaximumDiagnosticMinutes,
    ErrorThreshold = value.ErrorThreshold, ErrorWindowMinutes = value.ErrorWindowMinutes };
}
