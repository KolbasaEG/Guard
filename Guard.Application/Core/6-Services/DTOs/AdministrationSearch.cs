using Guard.Core.Services.Exports;

namespace Guard.Core.Services.DTOs;

public sealed record RoleSearchRequest(int Skip = 0, int Take = 20, string? OrderBy = null, ExportFilter? Filter = null);
public sealed record RoleSearchResult(IReadOnlyList<RoleListDto> Items, int Count);
public sealed record LogSearchRequest(int Skip = 0, int Take = 100, string? OrderBy = null, ExportFilter? Filter = null);
public sealed record LogSearchResult(IReadOnlyList<LogEntryDto> Items, int Count);
// Radzen keys rows by the item. Identical events in the keyless table must remain separate rows.
public sealed class LogEntryDto(DateTime timestamp, string? level, string? layer, string? message, string? exception, string? userId, string? clientIp,
  Guid id = default, string? properties = null, string? messageTemplate = null,
  string? correlationId = null, string? sourceContext = null, Guid? diagnosticSessionId = null, string? runId = null, string? fingerprint = null,
  string? applicationVersion = null, string? instanceId = null)
{
  public string? ApplicationVersion { get; } = applicationVersion;
  public string? InstanceId { get; } = instanceId;
  public string? CorrelationId { get; } = correlationId;
  public string? SourceContext { get; } = sourceContext;
  public Guid? DiagnosticSessionId { get; } = diagnosticSessionId;
  public string? RunId { get; } = runId;
  public string? Fingerprint { get; } = fingerprint;
  public Guid Id { get; } = id;
  public string? Properties { get; } = properties;
  public string? MessageTemplate { get; } = messageTemplate;
  public DateTime Timestamp { get; } = timestamp;
  public string? Level { get; } = level;
  public string? Layer { get; } = layer;
  public string? Message { get; } = message;
  public string? Exception { get; } = exception;
  public string? UserId { get; } = userId;
  public string? ClientIp { get; } = clientIp;
}
