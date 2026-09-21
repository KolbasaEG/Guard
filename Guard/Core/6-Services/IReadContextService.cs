using Guard.Core.Entities;

namespace Guard.Core.Services;

public interface IReadContextService
{
  // Универсальный доступ к репозиторию любой сущности по запросу
  IReadRepository<T> GetRepository<T>() where T : class;
}