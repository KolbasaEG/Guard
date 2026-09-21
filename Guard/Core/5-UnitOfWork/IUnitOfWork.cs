using Core.Repositories;
using Guard.Core.Repositories;

public interface IUnitOfWork : IAsyncDisposable, IDisposable
{
  IGenericRepository<T> Repository<T>() where T : class;

  Task<int> SaveChangesAsync(CancellationToken ct = default);
  int SaveChanges();

  // Транзакции (низкоуровневые)
  Task BeginTransactionAsync(CancellationToken ct = default);
  Task CommitTransactionAsync(CancellationToken ct = default);
  Task RollbackTransactionAsync(CancellationToken ct = default);

  void BeginTransaction();
  void CommitTransaction();
  void RollbackTransaction();

  // Удобные обёртки
  Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);
  Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken ct = default);
}