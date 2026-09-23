namespace Guard.Core.Repositories;

/// <summary>
/// Универсальный базовый репозиторий для сущностей, не унаследованных от BaseEntity.
/// Содержит только базовые CRUD-операции и работу с IQueryable.
/// </summary>
public interface IBasicRepository<T> where T : class
{
  // ==================== IQueryable ====================

  /// <summary>
  /// Возвращает IQueryable для построения сложных запросов.
  /// </summary>
  IQueryable<T> Query();

  // ==================== Read ====================

  /// <summary>
  /// Получает сущность по первичному ключу (или составному ключу).
  /// </summary>
  T? GetById(params object[] keyValues);

  /// <summary>
  /// Получает сущность по первичному ключу (или составному ключу) асинхронно.
  /// </summary>
  Task<T?> GetByIdAsync(object[] keyValues, CancellationToken ct = default);

  /// <summary>
  /// Получает все сущности (без отслеживания).
  /// </summary>
  IReadOnlyList<T> GetAll();

  /// <summary>
  /// Получает все сущности (без отслеживания, асинхронно).
  /// </summary>
  Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

  // ==================== Create ====================

  /// <summary>
  /// Добавляет сущность в контекст.
  /// </summary>
  void Add(T entity);

  /// <summary>
  /// Добавляет сущность в контекст (асинхронно).
  /// </summary>
  Task AddAsync(T entity, CancellationToken ct = default);

  /// <summary>
  /// Добавляет несколько сущностей.
  /// </summary>
  void AddRange(IEnumerable<T> entities);

  /// <summary>
  /// Добавляет несколько сущностей (асинхронно).
  /// </summary>
  Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

  // ==================== Update ====================

  /// <summary>
  /// Обновляет сущность с безопасной обработкой уже отслеживаемых контекстом экземпляров.
  /// </summary>
  void Update(T entity);

  /// <summary>
  /// Асинхронно обновляет сущность.
  /// </summary>
  Task UpdateAsync(T entity, CancellationToken ct = default);

  /// <summary>
  /// Обновляет коллекцию сущностей.
  /// </summary>
  void UpdateRange(IEnumerable<T> entities);

  /// <summary>
  /// Асинхронно обновляет коллекцию сущностей.
  /// </summary>
  Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

  // ==================== Delete (физическое) ====================

  /// <summary>
  /// Физическое удаление сущности из базы.
  /// </summary>
  void Delete(T entity);

  /// <summary>
  /// Асинхронное физическое удаление сущности.
  /// </summary>
  Task DeleteAsync(T entity, CancellationToken ct = default);

  /// <summary>
  /// Физическое удаление нескольких сущностей.
  /// </summary>
  void DeleteRange(IEnumerable<T> entities);

  /// <summary>
  /// Асинхронное физическое удаление нескольких сущностей.
  /// </summary>
  Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

  // ==================== Дополнительные методы ====================

  /// <summary>
  /// Проверяет существование сущности по ключу.
  /// </summary>
  bool Exists(params object[] keyValues);

  /// <summary>
  /// Проверяет существование сущности по ключу (асинхронно).
  /// </summary>
  Task<bool> ExistsAsync(object[] keyValues, CancellationToken ct = default);

  /// <summary>
  /// Возвращает общее количество записей.
  /// </summary>
  int Count();

  /// <summary>
  /// Возвращает общее количество записей (асинхронно).
  /// </summary>
  Task<int> CountAsync(CancellationToken ct = default);
}