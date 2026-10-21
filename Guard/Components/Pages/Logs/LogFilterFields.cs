namespace Guard.Components.Pages.Logs;

public sealed class LogFilterFields
{
  public DateTime Timestamp { get; set; }
  public string? Level { get; set; }
  public string? Layer { get; set; }
  public string? Message { get; set; }
  public string? Exception { get; set; }
  public string? UserId { get; set; }
  public string? ClientIp { get; set; }
  public string? CorrelationId { get; set; }
  public string? SourceContext { get; set; }
  public Guid? DiagnosticSessionId { get; set; }
  public string? RunId { get; set; }
  public string? Fingerprint { get; set; }
}
