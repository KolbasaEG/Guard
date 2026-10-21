using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

public interface ITechnicalLogService
{
  Task<TechnicalLogState> GetAsync(CancellationToken ct = default);
  Task<TechnicalLogState> SaveAsync(Guid version, TechnicalLogSettings settings, CancellationToken ct = default);
  Task<TechnicalLogState> StartAsync(Guid version, DiagnosticLogRequest request, CancellationToken ct = default);
  Task<TechnicalLogState> StopAsync(Guid version, CancellationToken ct = default);
  Task<IReadOnlyList<TechnicalErrorGroup>> ErrorsAsync(CancellationToken ct = default);
  Task<TechnicalLogExport> ExportAsync(TechnicalLogExportRequest request, CancellationToken ct = default);
}
