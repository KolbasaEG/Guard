using Guard.Core.Entities;

namespace Guard.Core.Services;

/// <summary>
/// Сервис для управления справочником типов органов (OrganType).
/// </summary>
public interface IOrganTypeService
{
  // ==================== Read Operations ====================

  /// <summary>
  /// Выполнение произвольного LINQ-запроса над справочником типов органов (Read-only context).
  /// </summary>
  Task<TResult> QueryOrganTypesAsync<TResult>(Func<IQueryable<OrganType>, Task<TResult>> query, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает тип органа по его идентификатору.
  /// </summary>
  /// <param name="id">Идентификатор типа органа</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Экземпляр <see cref="OrganType"/> или <c>null</c>, если запись не найдена.</returns>
  Task<OrganType?> GetByIdAsync(int id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает тип органа по его коду в классификаторе.
  /// </summary>
  /// <param name="code">Код типа органа</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Экземпляр <see cref="OrganType"/> или <c>null</c>, если запись не найдена.</returns>
  Task<OrganType?> GetByCodeAsync(int code, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает полный список типов органов.
  /// </summary>
  /// <param name="ct">Токен отмены операции</param>
  Task<IReadOnlyList<OrganType>> GetAllAsync(CancellationToken ct = default);

  /// <summary>
  /// Проверяет уникальность кода типа органа в классификаторе.
  /// </summary>
  /// <param name="code">Код для проверки</param>
  /// <param name="excludeId">Исключаемый ID (при редактировании существующей записи)</param>
  /// <param name="ct">Токен отмены операции</param>
  Task<bool> IsCodeUniqueAsync(int code, int? excludeId = null, CancellationToken ct = default);


  // ==================== Write Operations ====================

  /// <summary>
  /// Асинхронно создает новую запись типа органа.
  /// </summary>
  /// <param name="organType">Заполненная модель типа органа</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Идентификатор созданной записи.</returns>
  Task<int> CreateAsync(OrganType organType, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно обновляет данные типа органа.
  /// </summary>
  /// <param name="organType">Модель типа органа с обновлёнными данными</param>
  /// <param name="ct">Токен отмены операции</param>
  Task UpdateAsync(OrganType organType, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно удаляет запись типа органа из базы данных.
  /// </summary>
  /// <param name="id">Идентификатор типа органа</param>
  /// <param name="ct">Токен отмены операции</param>
  Task DeleteAsync(int id, CancellationToken ct = default);
}