using Guard.Core.Entities;
using Guard.Core.Enums;

namespace Guard.Core.Services;

/// <summary>
/// Сервис для управления персоналом (Personal).
/// </summary>
public interface IPersonalService
{
  /// <summary>
  /// Асинхронно получает список всех активных подразделений (исключая удалённые и архивированные).
  /// </summary>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Список активных подразделений, отсортированный по наименованию.</returns>
  Task<IReadOnlyList<Subdivision>> GetAllActiveSubdivisionsAsync(CancellationToken ct = default);


  // ==================== Read Operations ====================

  /// <summary>
  /// Выполняет произвольный асинхронный запрос к сущности Personal без отслеживания изменений.
  /// </summary>
  Task<TResult> QueryPersonalsAsync<TResult>(Func<IQueryable<Personal>, Task<TResult>> query, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает запись сотрудника по его уникальному идентификатору.
  /// </summary>
  /// <param name="id">Уникальный идентификатор сотрудника (Guid)</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Экземпляр <see cref="Personal"/> или <c>null</c>, если запись не найдена.</returns>
  Task<Personal?> GetByIdAsync(Guid id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает список всех активных сотрудников (исключая удалённых и архивированных).
  /// </summary>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Список активных сотрудников, отсортированный по фамилии и имени.</returns>
  Task<IReadOnlyList<Personal>> GetAllActiveAsync(CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает список сотрудников, привязанных к конкретному подразделению по ID.
  /// </summary>
  /// <param name="subdivisionId">Идентификатор подразделения</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Список сотрудников подразделения.</returns>
  Task<IReadOnlyList<Personal>> GetBySubdivisionIdAsync(Guid subdivisionId, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает список сотрудников по пути подразделения (Path) с учетом иерархического режима выборки.
  /// </summary>
  /// <param name="targetPath">Путь подразделения (например, "/1/4/")</param>
  /// <param name="mode">Режим выборки (только текущее подразделение или включая всех подчиненных)</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Список сотрудников, соответствующих критериям выборки.</returns>
  Task<IReadOnlyList<Personal>> GetBySubdivisionPathAsync(
      string targetPath,
      SubdivisionHierarchyMode mode = SubdivisionHierarchyMode.IncludeChildren,
      CancellationToken ct = default);

  // ==================== Write Operations ====================

  /// <summary>
  /// Асинхронно создает новую запись сотрудника.
  /// </summary>
  /// <param name="Personal">Заполненная модель персонала</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Уникальный идентификатор созданной записи (UUIDv7).</returns>
  Task<Guid> CreateAsync(Personal Personal, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно обновляет бизнес-поля существующей записи сотрудника с сохранением системных данных аудита.
  /// </summary>
  /// <param name="Personal">Модель персонала с обновлёнными данными</param>
  /// <param name="ct">Токен отмены операции</param>
  Task UpdateAsync(Personal Personal, CancellationToken ct = default);

  // ==================== Status Management ====================

  /// <summary>
  /// Асинхронно выполняет мягкое удаление сотрудника (Status = Deleted).
  /// </summary>
  /// <param name="id">Идентификатор сотрудника</param>
  /// <param name="ct">Токен отмены операции</param>
  Task SoftDeleteAsync(Guid id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно переводит запись сотрудника в архив (Status = Archived).
  /// </summary>
  /// <param name="id">Идентификатор сотрудника</param>
  /// <param name="ct">Токен отмены операции</param>
  Task ArchiveAsync(Guid id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно блокирует запись сотрудника от редактирования.
  /// </summary>
  /// <param name="id">Идентификатор сотрудника</param>
  /// <param name="ct">Токен отмены операции</param>
  Task BlockAsync(Guid id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно снимает блокировку с записи сотрудника.
  /// </summary>
  /// <param name="id">Идентификатор сотрудника</param>
  /// <param name="ct">Токен отмены операции</param>
  Task UnblockAsync(Guid id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно восстанавливает запись сотрудника из удалённых или архива (Status = Modified).
  /// </summary>
  /// <param name="id">Идентификатор сотрудника</param>
  /// <param name="ct">Токен отмены операции</param>
  Task RestoreAsync(Guid id, CancellationToken ct = default);
}