using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

public interface IApiDiagnosticService
{
  Task<ApiDiagnosticResult> RunAsync(DateOnly updatedSince, CancellationToken ct = default, IProgress<string>? progress = null);
}
