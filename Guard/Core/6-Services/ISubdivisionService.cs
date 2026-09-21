using Guard.Core.Entities;

namespace Guard.Core.Services;

/// <summary>
/// Сервис для управления подразделениями (Subdivision).
/// </summary>
public interface ISubdivisionService
{
  // ==================== Read Operations ====================

  Task<TResult> QuerySubdivisionsAsync<TResult>(Func<IQueryable<Subdivision>, Task<TResult>> query, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает подразделение по его уникальному идентификатору.
  /// </summary>
  /// <param name="id">Уникальный идентификатор подразделения (Guid)</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Экземпляр <see cref="Subdivision"/> или <c>null</c>, если запись не найдена.</returns>
  Task<Subdivision?> GetByIdAsync(Guid id, CancellationToken ct = default);

  /// <summary>
  /// Перемещает подразделение к новому родителю с каскадным пересчетом Path для всех потомков.
  /// </summary>
  /// <param name="id">Идентификатор перемещаемого подразделения</param>
  /// <param name="newParentId">ID нового родителя (null, если узел становится корневым)</param>
  /// <param name="ct">Токен отмены</param>
  Task MoveAsync(Guid id, Guid? newParentId, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает список всех активных подразделений (исключая удалённые и архивированные).
  /// </summary>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Список активных подразделений, отсортированный по наименованию.</returns>
  Task<IReadOnlyList<Subdivision>> GetAllActiveAsync(CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает список дочерних подразделений для указанного родителя.
  /// </summary>
  /// <param name="parentId">Идентификатор родительского подразделения (или <c>null</c> для корневых элементов)</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Список дочерних подразделений.</returns>
  Task<IReadOnlyList<Subdivision>> GetByParentIdAsync(Guid? parentId, CancellationToken ct = default);


  // ==================== Write Operations ====================

  /// <summary>
  /// Асинхронно создает новое подразделение.
  /// </summary>
  /// <param name="subdivision">Заполненная модель подразделения</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Уникальный идентификатор созданной записи (UUIDv7).</returns>
  Task<Guid> CreateAsync(Subdivision subdivision, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно обновляет бизнес-поля существующего подразделения с сохранением системных данных аудита.
  /// </summary>
  /// <param name="subdivision">Модель подразделения с обновлёнными данными</param>
  /// <param name="ct">Токен отмены операции</param>
  Task UpdateAsync(Subdivision subdivision, CancellationToken ct = default);


  // ==================== Status Management ====================

  /// <summary>
  /// Асинхронно выполняет мягкое удаление подразделения (Status = Deleted).
  /// </summary>
  /// <param name="id">Идентификатор подразделения</param>
  /// <param name="ct">Токен отмены операции</param>
  Task SoftDeleteAsync(Guid id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно переводит подразделение в архив (Status = Archived).
  /// </summary>
  /// <param name="id">Идентификатор подразделения</param>
  /// <param name="ct">Токен отмены операции</param>
  Task ArchiveAsync(Guid id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно блокирует подразделение от редактирования.
  /// </summary>
  /// <param name="id">Идентификатор подразделения</param>
  /// <param name="ct">Токен отмены операции</param>
  Task BlockAsync(Guid id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно снимает блокировку с подразделения.
  /// </summary>
  /// <param name="id">Идентификатор подразделения</param>
  /// <param name="ct">Токен отмены операции</param>
  Task UnblockAsync(Guid id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно восстанавливает подразделение из удалённых или архива (Status = Modified).
  /// </summary>
  /// <param name="id">Идентификатор подразделения</param>
  /// <param name="ct">Токен отмены операции</param>
  Task RestoreAsync(Guid id, CancellationToken ct = default);
}