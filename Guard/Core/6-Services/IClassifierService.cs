using Guard.Core.Entities;

namespace Guard.Core.Services;

/// <summary>
/// Сервис для управления классификаторами и универсальными справочниками (Classifier).
/// </summary>
public interface IClassifierService
{
  // ==================== Read Operations ====================

  /// <summary>
  /// Выполнение произвольного LINQ-запроса над классификаторами (Read-only context).
  /// </summary>
  Task<TResult> QueryClassifiersAsync<TResult>(
      Func<IQueryable<Classifier>, Task<TResult>> query,
      CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает элемент классификатора по его уникальному идентификатору.
  /// </summary>
  /// <param name="id">Уникальный идентификатор (int)</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Экземпляр <see cref="Classifier"/> или <c>null</c>, если запись не найдена.</returns>
  Task<Classifier?> GetByIdAsync(int id, CancellationToken ct = default);

  /// <summary>
  /// Получает все элементы классификатора по конкретному типу справочника.
  /// </summary>
  /// <param name="type">Тип справочника</param>
  /// <param name="activeOnly">Возвращать только активные элементы</param>
  /// <param name="ct">Токен отмены операции</param>
  Task<IReadOnlyList<Classifier>> GetByTypeAsync(int type, bool activeOnly = true, CancellationToken ct = default);

  /// <summary>
  /// Получает все элементы классификатора по имени справочника.
  /// </summary>
  /// <param name="classifierName">Наименование классификатора</param>
  /// <param name="activeOnly">Возвращать только активные элементы</param>
  /// <param name="ct">Токен отмены операции</param>
  Task<IReadOnlyList<Classifier>> GetByClassifierNameAsync(string classifierName, bool activeOnly = true, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает список всех классификаторов.
  /// </summary>
  /// <param name="ct">Токен отмены операции</param>
  Task<IReadOnlyList<Classifier>> GetAllAsync(CancellationToken ct = default);

  /// <summary>
  /// Проверяет, уникален ли код значения в рамках заданного типа справочника.
  /// </summary>
  /// <param name="type">Тип справочника</param>
  /// <param name="code">Код значения внутри справочника</param>
  /// <param name="excludeId">Исключаемый ID (при редактировании существующей записи)</param>
  /// <param name="ct">Токен отмены операции</param>
  Task<bool> IsCodeUniqueInTypeAsync(int type, int code, int? excludeId = null, CancellationToken ct = default);

  // ==================== Write Operations ====================

  /// <summary>
  /// Асинхронно создает новую запись классификатора.
  /// </summary>
  /// <param name="classifier">Заполненная модель классификатора</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Идентификатор созданной записи.</returns>
  Task<int> CreateAsync(Classifier classifier, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно обновляет данные существующего классификатора.
  /// </summary>
  /// <param name="classifier">Модель классификатора с обновлёнными данными</param>
  /// <param name="ct">Токен отмены операции</param>
  Task UpdateAsync(Classifier classifier, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно переключает статус активности классификатора (IsActive).
  /// </summary>
  /// <param name="id">Идентификатор классификатора</param>
  /// <param name="isActive">Новый статус активности</param>
  /// <param name="ct">Токен отмены операции</param>
  Task SetActiveStatusAsync(int id, bool isActive, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно выполняет физическое удаление записи классификатора.
  /// </summary>
  /// <param name="id">Идентификатор записи</param>
  /// <param name="ct">Токен отмены операции</param>
  Task DeleteAsync(int id, CancellationToken ct = default);
}