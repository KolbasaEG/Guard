namespace Guard.Components.Pages.Audit;

public sealed class AuditFilterFields
{
  public DateTime OccurredAtUtc { get; set; }
  public string? Actor { get; set; }
  public string? Outcome { get; set; }
  public string? ClientIp { get; set; }
  public string? CorrelationId { get; set; }
  public string? ObjectType { get; set; }
  public string? ObjectId { get; set; }
}
