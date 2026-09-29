using Guard.Core.Entities;

namespace Guard.Core.Services;

/// <summary>
/// Сервис для управления охраняемыми объектами (ProtectedObject).
/// </summary>
public interface IProtectedObjectService
{
  // ==================== Read Operations ====================

  /// <summary>
  /// Выполнение произвольного LINQ-запроса над охраняемыми объектами (Read-only context).
  /// </summary>
  Task<TResult> QueryObjectsAsync<TResult>(Func<IQueryable<ProtectedObject>, Task<TResult>> query, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает охраняемый объект по его уникальному идентификатору.
  /// </summary>
  /// <param name="id">Уникальный идентификатор объекта (Guid)</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Экземпляр <see cref="ProtectedObject"/> или <c>null</c>, если запись не найдена.</returns>
  Task<ProtectedObject?> GetByIdAsync(Guid id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает список всех активных охраняемых объектов (исключая удалённые).
  /// </summary>
  /// <param name="ct">Токен отмены операции</param>
  Task<IReadOnlyList<ProtectedObject>> GetAllActiveAsync(CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает список охраняемых объектов, привязанных к конкретному подразделению.
  /// </summary>
  /// <param name="subdivisionId">Идентификатор подразделения</param>
  /// <param name="ct">Токен отмены операции</param>
  Task<IReadOnlyList<ProtectedObject>> GetBySubdivisionIdAsync(Guid subdivisionId, CancellationToken ct = default);

  /// <summary>
  /// Проверяет, уникально ли наименование объекта в рамках подразделения.
  /// </summary>
  /// <param name="name">Наименование объекта</param>
  /// <param name="subdivisionId">Идентификатор подразделения</param>
  /// <param name="excludeId">Исключаемый ID (при редактировании существующей записи)</param>
  /// <param name="ct">Токен отмены операции</param>
  Task<bool> IsNameUniqueAsync(string name, Guid? subdivisionId, Guid? excludeId = null, CancellationToken ct = default);


  // ==================== Write Operations ====================

  /// <summary>
  /// Асинхронно создает новый охраняемый объект.
  /// </summary>
  /// <param name="protectedObject">Заполненная модель охраняемого объекта</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Уникальный идентификатор созданной записи (Guid).</returns>
  Task<Guid> CreateAsync(ProtectedObject protectedObject, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно обновляет данные существующего охраняемого объекта.
  /// </summary>
  /// <param name="protectedObject">Модель объекта с обновлёнными данными</param>
  /// <param name="ct">Токен отмены операции</param>
  Task UpdateAsync(ProtectedObject protectedObject, CancellationToken ct = default);


  // ==================== Status Management ====================

  /// <summary>
  /// Асинхронно выполняет мягкое удаление объекта (Status = Deleted).
  /// </summary>
  /// <param name="id">Идентификатор объекта</param>
  /// <param name="ct">Токен отмены операции</param>
  Task SoftDeleteAsync(Guid id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно переводит объект в архив (Status = Archived).
  /// </summary>
  /// <param name="id">Идентификатор объекта</param>
  /// <param name="ct">Токен отмены операции</param>
  Task ArchiveAsync(Guid id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно восстанавливает объект из удалённых или архива (Status = Modified).
  /// </summary>
  /// <param name="id">Идентификатор объекта</param>
  /// <param name="ct">Токен отмены операции</param>
  Task RestoreAsync(Guid id, CancellationToken ct = default);
}