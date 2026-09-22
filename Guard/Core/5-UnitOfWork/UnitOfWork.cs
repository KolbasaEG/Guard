using Core.Repositories;
using Guard.Core.Contexts;
using Guard.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

public class UnitOfWork : IUnitOfWork
{
  private readonly ApplicationDbContext _context;
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<UnitOfWork> _logger;
  private readonly Dictionary<Type, object> _repositories = [];
  private IDbContextTransaction? _currentTransaction;


  public UnitOfWork(ApplicationDbContext context, IServiceProvider serviceProvider, ILogger<UnitOfWork> logger)
  {
    _context = context;
    _serviceProvider = serviceProvider;
    _logger = logger;
  }

  // ==================== Репозитории ====================

  public IGenericRepository<T> Repository<T>() where T : class
  {
    var type = typeof(T);

    if (!_repositories.TryGetValue(type, out var repo))
    {
      _logger.LogDebug("Инициализация и кэширование репозитория для сущности '{EntityType}'", type.Name);
      repo = _serviceProvider.GetRequiredService<IGenericRepository<T>>();
      _repositories[type] = repo;
    }

    return (IGenericRepository<T>)_repositories[type];
  }

  // ==================== SaveChanges ====================
  public async Task<int> SaveChangesAsync(CancellationToken ct = default)
  {
    _logger.LogDebug("Асинхронное сохранение изменений в БД...");
    try
    {
      var result = await _context.SaveChangesAsync(ct);
      _logger.LogDebug("Успешно сохранено сущностей в БД: {Count}", result);
      return result;
    }
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
      var result = _context.SaveChanges();
      _logger.LogDebug("Успешно сохранено сущностей в БД: {Count}", result);
      return result;
    }
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
      await _context.SaveChangesAsync(ct);

      _logger.LogInformation("Фиксация асинхронной транзакции...");
      await _currentTransaction.CommitAsync(ct);
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
      _context.SaveChanges();

      _logger.LogInformation("Фиксация синхронной транзакции...");
      _currentTransaction.Commit();
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
    var strategy = _context.Database.CreateExecutionStrategy();

    await strategy.ExecuteAsync(async () =>
    {
      _logger.LogDebug("Запуск выполнения асинхронной операции в транзакции с ретрай-стратегией EF Core");
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
    });
  }

  public async Task<TResult> ExecuteInTransactionAsync<TResult>(
      Func<Task<TResult>> action,
      CancellationToken ct = default)
  {
    var strategy = _context.Database.CreateExecutionStrategy();

    return await strategy.ExecuteAsync(async () =>
    {
      _logger.LogDebug("Запуск выполнения асинхронной операции (с результатом) в транзакции с ретрай-стратегией EF Core");
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
    });
  }

  // ==================== Dispose ====================
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