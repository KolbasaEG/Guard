using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

public interface IRemoteClassifierService
{
  /// <summary>
  /// Получает полный список классификаторов с удаленного ресурса.
  /// </summary>
  Task<IReadOnlyList<RemoteClassifierDto>> FetchClassifiersAsync(CancellationToken ct = default);

  /// <summary>
  /// Загружает элементы с внешнего API и синхронизирует их с локальной базой данных.
  /// </summary>
  Task SyncClassifiersAsync(CancellationToken ct = default);
}