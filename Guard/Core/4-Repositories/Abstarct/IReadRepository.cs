using Guard.Core.Entities;

public interface IReadRepository<T> where T : class
{
  Task<TResult> QueryAsync<TResult>(Func<IQueryable<T>, Task<TResult>> query, CancellationToken ct = default);  
  Task<T?> GetByIdAsync(object id, CancellationToken ct = default);
  Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

  Task<bool> ExistsAsync(object id, CancellationToken ct = default);
  Task<int> CountAsync(CancellationToken ct = default);
}