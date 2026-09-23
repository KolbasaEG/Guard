using Guard.Core.Entities;

namespace Guard.Core.Services;

public interface IClassifierService
{
  IQueryable<Classifier> Query();
  Classifier? GetById(object id);
  Task<Classifier?> GetByIdAsync(object id, CancellationToken ct = default);
  IReadOnlyList<Classifier> GetAll();
  Task<IReadOnlyList<Classifier>> GetAllAsync(CancellationToken ct = default);
  void Add(Classifier entity);
  Task<int> AddAsync(Classifier entity);

  void Update(Classifier entity);
  void SoftDelete(Classifier entity);
  void Restore(Classifier entity);
  void ChangeStatus(Classifier entity, bool newStatus);

  void Delete(Classifier entity);
  bool Exists(object id);
  Task<bool> ExistsAsync(object id, CancellationToken ct = default);
  int Count();
  Task<int> CountAsync(CancellationToken ct = default);


}