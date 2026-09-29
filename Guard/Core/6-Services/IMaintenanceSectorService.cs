using Guard.Core.Entities;

namespace Guard.Core.Services;

/// <summary>
/// Сервис для управления участками обслуживания (MaintenanceSector).
/// </summary>
public interface IMaintenanceSectorService
{
  // ==================== Read Operations ====================

  /// <summary>
  /// Выполнение произвольного LINQ-запроса над участками обслуживания (Read-only context).
  /// </summary>
  Task<TResult> QuerySectorsAsync<TResult>(Func<IQueryable<MaintenanceSector>, Task<TResult>> query, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает участок обслуживания по его уникальному идентификатору.
  /// </summary>
  /// <param name="id">Уникальный идентификатор участка (Guid)</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Экземпляр <see cref="MaintenanceSector"/> или <c>null</c>, если запись не найдена.</returns>
  Task<MaintenanceSector?> GetByIdAsync(Guid id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает список всех активных участков обслуживания (исключая удалённые).
  /// </summary>
  /// <param name="ct">Токен отмены операции</param>
  Task<IReadOnlyList<MaintenanceSector>> GetAllActiveAsync(CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает список участков обслуживания, привязанных к конкретному подразделению.
  /// </summary>
  /// <param name="subdivisionId">Идентификатор подразделения</param>
  /// <param name="ct">Токен отмены операции</param>
  Task<IReadOnlyList<MaintenanceSector>> GetBySubdivisionIdAsync(Guid subdivisionId, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает список участков, закpепленных за конкретным ответственным сотрудником.
  /// </summary>
  /// <param name="responsiblePersonalId">Идентификатор ответственного сотрудника</param>
  /// <param name="ct">Токен отмены операции</param>
  Task<IReadOnlyList<MaintenanceSector>> GetByResponsiblePersonalIdAsync(Guid responsiblePersonalId, CancellationToken ct = default);

  /// <summary>
  /// Проверяет, уникально ли наименование участка в рамках подразделения.
  /// </summary>
  /// <param name="name">Наименование участка</param>
  /// <param name="subdivisionId">Идентификатор подразделения</param>
  /// <param name="excludeId">Исключаемый ID (при редактировании существующей записи)</param>
  /// <param name="ct">Токен отмены операции</param>
  Task<bool> IsNameUniqueAsync(string name, Guid? subdivisionId, Guid? excludeId = null, CancellationToken ct = default);


  // ==================== Write Operations ====================

  /// <summary>
  /// Асинхронно создает новый участок обслуживания.
  /// </summary>
  /// <param name="sector">Заполненная модель участка обслуживания</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Уникальный идентификатор созданной записи (Guid).</returns>
  Task<Guid> CreateAsync(MaintenanceSector sector, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно обновляет данные существующего участка обслуживания.
  /// </summary>
  /// <param name="sector">Модель участка с обновлёнными данными</param>
  /// <param name="ct">Токен отмены операции</param>
  Task UpdateAsync(MaintenanceSector sector, CancellationToken ct = default);


  // ==================== Status Management ====================

  /// <summary>
  /// Асинхронно выполняет мягкое удаление участка обслуживания (Status = Deleted).
  /// </summary>
  /// <param name="id">Идентификатор участка</param>
  /// <param name="ct">Токен отмены операции</param>
  Task SoftDeleteAsync(Guid id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно переводит участок обслуживания в архив (Status = Archived).
  /// </summary>
  /// <param name="id">Идентификатор участка</param>
  /// <param name="ct">Токен отмены операции</param>
  Task ArchiveAsync(Guid id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно восстанавливает участок обслуживания из удалённых или архива (Status = Modified).
  /// </summary>
  /// <param name="id">Идентификатор участка</param>
  /// <param name="ct">Токен отмены операции</param>
  Task RestoreAsync(Guid id, CancellationToken ct = default);
}