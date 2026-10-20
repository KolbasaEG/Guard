using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Guard.Core.Services;
using Npgsql;
using Guard.Core.Logging;

public class UnitOfWork : IUnitOfWork
{
  private readonly ApplicationDbContext _context;
  private readonly ILogger<UnitOfWork> _logger;
  private readonly Dictionary<Type, object> _repositories = [];
  private readonly Dictionary<Type, object> _basicRepositories = [];
  private IDbContextTransaction? _currentTransaction;
  private readonly IAuditService? audit;
  private readonly List<AuditEventData> pendingAudit = [];


  public UnitOfWork(ApplicationDbContext context, IServiceProvider serviceProvider, ILogger<UnitOfWork> logger)
  {
    _context = context;
    _logger = logger;
    audit = serviceProvider?.GetService<IAuditService>();
    AuditTransaction.Attach(_context, audit);
  }

  // ==================== Репозитории ====================
  /// <summary>
  /// Для сущностей с BaseEntity (IGenericRepository)
  /// </summary>
  public IGenericRepository<T> BaseEntityRepository<T>() where T : BaseEntity
  {
    var type = typeof(T);

    if (!_repositories.TryGetValue(type, out var repo))
    {
      _logger.LogDebug("Инициализация IGenericRepository для '{EntityType}'", type.Name);
      repo = new GenericRepository<T>(_context);
      _repositories[type] = repo;
    }

    return (IGenericRepository<T>)_repositories[type];
  }

  /// <summary>
  /// Для всех остальных сущностей (IBasicRepository)
  /// </summary>
  public IBasicRepository<T> BasicRepository<T>() where T : class
  {
    var type = typeof(T);

    if (!_basicRepositories.TryGetValue(type, out var repo))
    {
      _logger.LogDebug("Инициализация IBasicRepository для '{EntityType}'", type.Name);
      repo = new BasicRepository<T>(_context);
      _basicRepositories[type] = repo;
    }

    return (IBasicRepository<T>)_basicRepositories[type];
  }

  // ==================== SaveChanges ====================
  public async Task<int> SaveChangesAsync(CancellationToken ct = default)
  {
    _logger.LogDebug("Асинхронное сохранение изменений в БД...");
    try
    {
      var captured = CaptureAudit();
      var result = await _context.SaveChangesAsync(ct);
      CompleteAudit(captured);
      if (_currentTransaction == null) await PublishAuditAsync();
      _logger.LogDebug("Успешно сохранено сущностей в БД: {Count}", result);
      return result;
    }
    catch (DbUpdateConcurrencyException) { throw new EntityConflictException(); }
    catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
    { throw new ArgumentException("Запись с такими ключевыми значениями уже существует.", ex); }
    catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation })
    { throw new ArgumentException("Связанная запись недоступна или используется другими записями. Обновите данные.", ex); }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Ошибка при асинхронном сохранении изменений в БД");
      throw;
    }
  }
  public int SaveChanges()
  {
    _logger.LogDebug("Синхронное сохранение изменений в БД...");
    try
    {
      var captured = CaptureAudit();
      var result = _context.SaveChanges();
      CompleteAudit(captured);
      if (_currentTransaction == null) PublishAudit();
      _logger.LogDebug("Успешно сохранено сущностей в БД: {Count}", result);
      return result;
    }
    catch (DbUpdateConcurrencyException) { throw new EntityConflictException(); }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Ошибка при синхронном сохранении изменений в БД");
      throw;
    }
  }

  // Выполнение сырых SQL-команд / скриптов
  public async Task<int> ExecuteSqlRawAsync(string sql, CancellationToken ct = default)
  {
    return await _context.Database.ExecuteSqlRawAsync(sql, ct);
  }

  public async Task<int> ExecuteSqlRawAsync(string sql, IEnumerable<object> parameters, CancellationToken ct = default)
  {
    return await _context.Database.ExecuteSqlRawAsync(sql, parameters, ct);
  }

  // ==================== Async Transactions ====================
  public async Task BeginTransactionAsync(CancellationToken ct = default)
  {
    if (_currentTransaction is not null)
    {
      _logger.LogWarning("Попытка повторного открытия асинхронной транзакции");
      throw new InvalidOperationException("Транзакция уже открыта.");
    }

    _logger.LogInformation("Открытие новой асинхронной транзакции...");
    _currentTransaction = await _context.Database.BeginTransactionAsync(ct);
  }
  public async Task CommitTransactionAsync(CancellationToken ct = default)
  {
    if (_currentTransaction is null)
    {
      _logger.LogWarning("Попытка фиксации отсутствующей транзакции");
      throw new InvalidOperationException("Нет активной транзакции.");
    }

    try
    {
      _logger.LogDebug("Сохранение изменений перед фиксацией транзакции...");
      await SaveChangesAsync(ct);

      _logger.LogInformation("Фиксация асинхронной транзакции...");
      await PublishAuditAsync();
      await AuditTransaction.CommitAsync(_currentTransaction, ct);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Ошибка при фиксации асинхронной транзакции. Запуск отката...");
      await RollbackTransactionAsync(CancellationToken.None);
      throw;
    }
    finally
    {
      await DisposeTransactionAsync();
    }
  }
  public async Task RollbackTransactionAsync(CancellationToken ct = default)
  {
    pendingAudit.Clear();
    if (_currentTransaction is null)
      return;

    try
    {
      _logger.LogWarning("Выполнение отката асинхронной транзакции...");
      await _currentTransaction.RollbackAsync(ct);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Ошибка при выполнении отката асинхронной транзакции");
    }
    finally
    {
      await DisposeTransactionAsync();
    }
  }

  // ==================== Sync Transactions ====================
  public void BeginTransaction()
  {
    if (_currentTransaction is not null)
    {
      _logger.LogWarning("Попытка повторного открытия синхронной транзакции");
      throw new InvalidOperationException("Транзакция уже открыта.");
    }

    _logger.LogInformation("Открытие новой синхронной транзакции...");
    _currentTransaction = _context.Database.BeginTransaction();
  }

  public void CommitTransaction()
  {
    if (_currentTransaction is null)
    {
      _logger.LogWarning("Попытка фиксации отсутствующей транзакции");
      throw new InvalidOperationException("Нет активной транзакции.");
    }

    try
    {
      _logger.LogDebug("Сохранение изменений перед фиксацией транзакции...");
      SaveChanges();

      _logger.LogInformation("Фиксация синхронной транзакции...");
      PublishAudit();
      AuditTransaction.CommitAsync(_currentTransaction, CancellationToken.None).GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Ошибка при фиксации синхронной транзакции. Запуск отката...");
      RollbackTransaction();
      throw;
    }
    finally
    {
      DisposeTransaction();
    }
  }

  public void RollbackTransaction()
  {
    pendingAudit.Clear();
    if (_currentTransaction is null)
      return;

    try
    {
      _logger.LogWarning("Выполнение отката синхронной транзакции...");
      _currentTransaction.Rollback();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Ошибка при выполнении отката синхронной транзакции");
    }
    finally
    {
      DisposeTransaction();
    }
  }

  // ==================== Удобные обёртки ====================
  public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
  {
    // Retry the whole application operation with a new unit of work, never this tracked context.
    ct.ThrowIfCancellationRequested();
    await BeginTransactionAsync(ct);
    try
    {
      await action();
      await CommitTransactionAsync(ct);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Сбой при выполнении действия в транзакции ExecuteInTransactionAsync");
      await RollbackTransactionAsync(CancellationToken.None);
      throw;
    }
  }

  public async Task<TResult> ExecuteInTransactionAsync<TResult>(
    Func<Task<TResult>> action,
    CancellationToken ct = default)
  {
    ct.ThrowIfCancellationRequested();
    await BeginTransactionAsync(ct);
    try
    {
      var result = await action();
      await CommitTransactionAsync(ct);
      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Сбой при выполнении действия с результатом в транзакции ExecuteInTransactionAsync");
      await RollbackTransactionAsync(CancellationToken.None);
      throw;
    }
  }

  // ==================== Dispose ====================
  private EntityChangeAudit? CaptureAudit()
  {
    return audit == null || _context.AuditWriter != null ? null : new EntityChangeAudit(_context);
  }

  private void CompleteAudit(EntityChangeAudit? captured)
  {
    if (captured != null) pendingAudit.AddRange(captured.Complete(_context.AuditActorId));
  }

  private static void AuditFailure(Exception error) => new InternalLogWriter().Write($"EntityAuditFailed ErrorType={error.GetType().Name}");

  private void PublishAudit()
  {
    var events = pendingAudit.ToArray(); pendingAudit.Clear();
    foreach (var entry in events) {
      audit?.Record(entry);
    }
  }

  private async Task PublishAuditAsync()
  {
    var events = pendingAudit.ToArray(); pendingAudit.Clear();
    foreach (var entry in events) {
      if (audit != null) await audit.RecordAsync(entry);
    }
  }

  private async Task DisposeTransactionAsync()
  {
    if (_currentTransaction is not null)
    {
      await _currentTransaction.DisposeAsync();
      _currentTransaction = null;
    }
  }

  private void DisposeTransaction()
  {
    if (_currentTransaction is not null)
    {
      _currentTransaction.Dispose();
      _currentTransaction = null;
    }
  }

  public async ValueTask DisposeAsync()
  {
    await DisposeTransactionAsync();
    await _context.DisposeAsync();
  }

  public void Dispose()
  {
    DisposeTransaction();
    _context.Dispose();
  }
}
