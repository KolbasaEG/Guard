using Guard.Core.Entities;
using Guard.Core.Repositories;

/// <summary>
/// Интерфейс паттерна Unit of Work (Единица работы).
/// Управляет транзакциями, выполнением SQL-скриптов и предоставляет единый доступ к репозиториям.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable, IDisposable
{
  /// <summary>
  /// Возвращает универсальный репозиторий для сущностей, унаследованных от <see cref="BaseEntity"/>.
  /// Поддерживает аудит, смену статусов, мягкое удаление и расширенный CRUD.
  /// </summary>
  /// <typeparam name="T">Тип сущности, унаследованной от BaseEntity</typeparam>
  /// <returns>Экземпляр <see cref="IGenericRepository{T}"/></returns>
  IGenericRepository<T> BaseEntityRepository<T>() where T : BaseEntity;

  /// <summary>
  /// Возвращает базовый репозиторий для сущностей, НЕ унаследованных от BaseEntity 
  /// (связывающие M2M-таблицы, простые справочники, системные сущности).
  /// </summary>
  /// <typeparam name="T">Тип сущности</typeparam>
  /// <returns>Экземпляр <see cref="IBasicRepository{T}"/></returns>
  IBasicRepository<T> BasicRepository<T>() where T : class;

  // ==================== Сохранение изменений ====================

  /// <summary>
  /// Асинхронно сохраняет все изменения, внесенные в рамках текущего контекста БД.
  /// </summary>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Количество записей, измененных в базе данных</returns>
  Task<int> SaveChangesAsync(CancellationToken ct = default);

  /// <summary>
  /// Синхронно сохраняет все изменения, внесенные в рамках текущего контекста БД.
  /// </summary>
  /// <returns>Количество записей, измененных в базе данных</returns>
  int SaveChanges();

  // ==================== Сырые SQL-запросы ====================

  /// <summary>
  /// Асинхронно выполняет сырую SQL-команду (например, UPDATE, DELETE или DDL).
  /// </summary>
  /// <param name="sql">Строка SQL-запроса</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Количество затронутых строк</returns>
  Task<int> ExecuteSqlRawAsync(string sql, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно выполняет сырую SQL-команду с параметрами для предотвращения SQL-инъекций.
  /// </summary>
  /// <param name="sql">Строка SQL-запроса с плейсхолдерами</param>
  /// <param name="parameters">Параметры запроса</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Количество затронутых строк</returns>
  Task<int> ExecuteSqlRawAsync(string sql, IEnumerable<object> parameters, CancellationToken ct = default);

  // ==================== Асинхронные транзакции ====================

  /// <summary>
  /// Асинхронно начинает новую явную транзакцию базы данных.
  /// </summary>
  /// <param name="ct">Токен отмены операции</param>
  /// <exception cref="InvalidOperationException">Выбрасывается, если транзакция уже открыта.</exception>
  Task BeginTransactionAsync(CancellationToken ct = default);

  /// <summary>
  /// Асинхронно сохраняет изменения и фиксирует текущую транзакцию.
  /// В случае ошибки автоматически выполняет откат (Rollback).
  /// </summary>
  /// <param name="ct">Токен отмены операции</param>
  /// <exception cref="InvalidOperationException">Выбрасывается, если нет активной транзакции.</exception>
  Task CommitTransactionAsync(CancellationToken ct = default);

  /// <summary>
  /// Асинхронно отменяет текущую транзакцию и откатывает не зафиксированные изменения.
  /// </summary>
  /// <param name="ct">Токен отмены операции</param>
  Task RollbackTransactionAsync(CancellationToken ct = default);

  // ==================== Синхронные транзакции ====================

  /// <summary>
  /// Синхронно начинает новую явную транзакцию базы данных.
  /// </summary>
  /// <exception cref="InvalidOperationException">Выбрасывается, если транзакция уже открыта.</exception>
  void BeginTransaction();

  /// <summary>
  /// Синхронно сохраняет изменения и фиксирует текущую транзакцию.
  /// В случае ошибки автоматически выполняет откат (Rollback).
  /// </summary>
  /// <exception cref="InvalidOperationException">Выбрасывается, если нет активной транзакции.</exception>
  void CommitTransaction();

  /// <summary>
  /// Синхронно отменяет текущую транзакцию и откатывает не зафиксированные изменения.
  /// </summary>
  void RollbackTransaction();

  // ==================== Удобные обёртки транзакций ====================

  /// <summary>
  /// Выполняет асинхронное действие внутри транзакции с автоматическим вызовом Commit/Rollback 
  /// и поддержкой стратегии повторных попыток EF Core (ExecutionStrategy).
  /// </summary>
  /// <param name="action">Выполняемое асинхронное действие</param>
  /// <param name="ct">Токен отмены операции</param>
  Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);

  /// <summary>
  /// Выполняет асинхронную функцию внутри транзакции с возвратом результата, 
  /// автоматическим вызовом Commit/Rollback и поддержкой стратегии повторных попыток EF Core.
  /// </summary>
  /// <typeparam name="TResult">Тип возвращаемого значения</typeparam>
  /// <param name="action">Выполняемая асинхронная функция</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Результат выполнения функции</returns>
  Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken ct = default);
}