using Guard.Core.Entities;
using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

public interface ISynchronizationService
{
  Task<DirectoryActuality> GetActualityAsync(string readPermission, CancellationToken ct = default);
  Task<SynchronizationOverview> GetOverviewAsync(bool classifiers = false, bool includeChildren = true, CancellationToken ct = default, bool allSubdivisions = false);
  /// <summary>Подтверждает сохранение настроек; ошибка последующей доставки аудита логируется.</summary>
  Task SaveSettingsAsync(DataManagementMode mode, bool enabled, TimeOnly time, Guid version, CancellationToken ct = default, string? apiBaseUrl = null, int? apiPageSize = null, string? apiUsername = null, string? apiPassword = null, long? apiHeadSubdivisionId = null, bool? completeSnapshotGuaranteed = null);
  /// <summary>Успешно сохранённый запуск остаётся успешным при ошибке последующего аудита; очередь возвращает Queued.</summary>
  Task<SynchronizationRunDto> RunAsync(CancellationToken ct = default, DateOnly? updatedSince = null, bool enqueue = false);
  Task<IReadOnlyList<SynchronizationRunDto>> GetHistoryAsync(CancellationToken ct = default);
  Task CheckConnectionAsync(CancellationToken ct = default);
}
