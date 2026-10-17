using Guard.Core.Enums;

namespace Guard.Core.Repositories;

/// <summary>
/// Универсальный репозиторий для работы с сущностями.
/// Поддерживает CRUD, работу со статусами (Status) и базовые запросы.
/// </summary>
public interface IGenericRepository<T> where T : class
{
  // ==================== IQueryable ====================

  /// <summary>
  /// Возвращает IQueryable для построения сложных запросов.
  /// </summary>
  IQueryable<T> Query();

  // ==================== Read ====================

  /// <summary>
  /// Получает сущность по идентификатору (синхронно).
  /// </summary>
  /// <param name="id">Первичный ключ сущности</param>
  T? GetById(object id);

  /// <summary>
  /// Получает сущность по идентификатору (асинхронно).
  /// </summary>
  /// <param name="id">Первичный ключ сущности</param>
  /// <param name="ct">Токен отмены</param>
  Task<T?> GetByIdAsync(object id, CancellationToken ct = default);

  /// <summary>
  /// Получает все сущности (без отслеживания).
  /// </summary>
  IReadOnlyList<T> GetAll();

  /// <summary>
  /// Получает все сущности (без отслеживания, асинхронно).
  /// </summary>
  /// <param name="ct">Токен отмены</param>
  Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

  // ==================== Create ====================

  /// <summary>
  /// Добавляет сущность в контекст.
  /// </summary>
  /// <param name="entity">Сущность для добавления</param>
  void Add(T entity);

  /// <summary>
  /// Добавляет сущность в контекст (асинхронно).
  /// </summary>
  /// <param name="entity">Сущность для добавления</param>
  /// <param name="ct">Токен отмены</param>
  Task AddAsync(T entity, CancellationToken ct = default);

  /// <summary>
  /// Добавляет несколько сущностей.
  /// </summary>
  /// <param name="entities">Коллекция сущностей</param>
  void AddRange(IEnumerable<T> entities);

  /// <summary>
  /// Добавляет несколько сущностей (асинхронно).
  /// </summary>
  /// <param name="entities">Коллекция сущностей</param>
  /// <param name="ct">Токен отмены</param>
  Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

  // ==================== Update ====================

  /// <summary>
  /// Обновляет сущность. Корректно работает с уже отслеживаемыми экземплярами.
  /// </summary>
  /// <param name="entity">Сущность для обновления</param>
  void Update(T entity);

  /// <summary>
  /// Асинхронно обновляет сущность с безопасным переносом бизнес-полей.
  /// </summary>
  /// <param name="entity">Сущность</param>
  /// <param name="ct">Токен отмены операции</param>
  Task UpdateAsync(T entity, CancellationToken ct = default);

  /// <summary>
  /// Обновляет несколько сущностей.
  /// </summary>
  /// <param name="entities">Коллекция сущностей</param>
  void UpdateRange(IEnumerable<T> entities);

  /// <summary>
  /// Асинхронно обновляет коллекцию сущностей.
  /// </summary>
  /// <param name="entities">Коллекция сущностей</param>
  /// <param name="ct">Токен отмены операции</param>
  Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

  // ==================== Status Management ====================

  /// <summary>
  /// Мягкое удаление сущности (устанавливает Status = Deleted).
  /// </summary>
  /// <param name="entity">Сущность</param>
  void SoftDelete(T entity);

  /// <summary>
  /// Асинхронно выполняет мягкое удаление сущности (устанавливает Status = Deleted).
  /// </summary>
  /// <param name="entity">Сущность</param>
  /// <param name="ct">Токен отмены операции</param>
  Task SoftDeleteAsync(T entity, CancellationToken ct = default);

  /// <summary>
  /// Архивирует сущность (Status = Archived).
  /// </summary>
  /// <param name="entity">Сущность</param>
  void Archive(T entity);

  /// <summary>
  /// Асинхронно архивирует сущность (Status = Archived).
  /// </summary>
  /// <param name="entity">Сущность</param>
  /// <param name="ct">Токен отмены операции</param>
  Task ArchiveAsync(T entity, CancellationToken ct = default);

  /// <summary>
  /// Блокирует сущность для редактирования.
  /// </summary>
  /// <param name="entity">Сущность</param>
  void Block(T entity);

  /// <summary>
  /// Асинхронно блокирует сущность для редактирования.
  /// </summary>
  /// <param name="entity">Сущность</param>
  /// <param name="ct">Токен отмены операции</param>
  Task BlockAsync(T entity, CancellationToken ct = default);

  /// <summary>
  /// Снимает блокировку с сущности.
  /// </summary>
  /// <param name="entity">Сущность</param>
  void Unblock(T entity);

  /// <summary>
  /// Асинхронно снимает блокировку с сущности.
  /// </summary>
  /// <param name="entity">Сущность</param>
  /// <param name="ct">Токен отмены операции</param>
  Task UnblockAsync(T entity, CancellationToken ct = default);

  /// <summary>
  /// Восстанавливает сущность из удалённых или архива (Status = Modified).
  /// </summary>
  /// <param name="entity">Сущность</param>
  void Restore(T entity);

  /// <summary>
  /// Асинхронно восстанавливает сущность из удалённых или архива (Status = Modified).
  /// </summary>
  /// <param name="entity">Сущность</param>
  /// <param name="ct">Токен отмены операции</param>
  Task RestoreAsync(T entity, CancellationToken ct = default);

  /// <summary>
  /// Универсальный метод смены статуса.
  /// </summary>
  /// <param name="entity">Сущность</param>
  /// <param name="newStatus">Новый статус</param>
  void ChangeStatus(T entity, Status newStatus);

  /// <summary>
  /// Универсальный асинхронный метод смены статуса.
  /// </summary>
  /// <param name="entity">Сущность</param>
  /// <param name="newStatus">Новый статус</param>
  /// <param name="ct">Токен отмены операции</param>
  
  Task ChangeStatusAsync(T entity, Status newStatus, CancellationToken ct = default);
  // ==================== Delete (физическое) ====================

  /// <summary>
  /// Физическое удаление сущности из базы.
  /// Использовать с осторожностью.
  /// </summary>
  /// <param name="entity">Сущность</param>
  void Delete(T entity);

  /// <summary>
  /// Асинхронное физическое удаление сущности из базы.
  /// Использовать с осторожностью.
  /// </summary>
  /// <param name="entity">Сущность</param>
  /// <param name="ct">Токен отмены операции</param>
  Task DeleteAsync(T entity, CancellationToken ct = default);

  /// <summary>
  /// Физическое удаление нескольких сущностей.
  /// </summary>
  /// <param name="entities">Коллекция сущностей</param>
  void DeleteRange(IEnumerable<T> entities);

  /// <summary>
  /// Асинхронное физическое удаление нескольких сущностей.
  /// </summary>
  /// <param name="entities">Коллекция сущностей</param>
  /// <param name="ct">Токен отмены операции</param>
  Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

  // ==================== Дополнительные методы ====================

  /// <summary>
  /// Проверяет существование сущности по идентификатору.
  /// </summary>
  /// <param name="id">Первичный ключ</param>
  bool Exists(object id);

  /// <summary>
  /// Проверяет существование сущности по идентификатору (асинхронно).
  /// </summary>
  /// <param name="id">Первичный ключ</param>
  /// <param name="ct">Токен отмены</param>
  Task<bool> ExistsAsync(object id, CancellationToken ct = default);

  /// <summary>
  /// Возвращает количество записей.
  /// </summary>
  int Count();

  /// <summary>
  /// Возвращает количество записей (асинхронно).
  /// </summary>
  /// <param name="ct">Токен отмены</param>
  Task<int> CountAsync(CancellationToken ct = default);
}