using Guard.Core.Services.DTOs;
using Guard.Core.Entities;
using Guard.Core.Enums;

namespace Guard.Core.Services;

/// <summary>
/// Сервис для управления подразделениями (Subdivision).
/// </summary>
public interface ISubdivisionService : ISubdivisionManagementService
{
  Task<string?> GetOwnSubdivisionPathAsync(CancellationToken ct = default);
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

  /// <summary>
  /// Получение типов органа.
  /// </summary>
  /// <param name="ct">Токен отмены операции</param>
  Task<IReadOnlyList<Classifier>> GetOrganTypesAsync(CancellationToken ct = default);

  /// <summary>
  /// Получение классификаторов по определенному типу.
  /// </summary>
  /// <param name="ct">Токен отмены операции</param>
  /// <param name="type">Тип классификатора</param>
  Task<IReadOnlyList<Classifier>> GetClassifiersByTypeAsync(ClassifierType type, CancellationToken ct = default);
}
