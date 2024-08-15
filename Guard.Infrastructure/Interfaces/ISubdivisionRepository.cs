using Guard.Domain.Entities;
using Guard.Domain.Specifications;

namespace Guard.Infrastructure.Interfaces
{
  /// <summary>
  /// Интерфейс репозитория для работы с подразделениями.
  /// </summary>
  public interface ISubdivisionRepository
  {
    /// <summary>
    /// Получение подразделения по его идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор подразделения.</param>
    /// <returns>Подразделение, соответствующее заданному идентификатору.</returns>
    Task<Subdivision> GetByIdAsync(Guid id);

    /// <summary>
    /// Получение всех подразделений.
    /// </summary>
    /// <returns>Коллекция всех подразделений.</returns>
    Task<IEnumerable<Subdivision>> GetAllAsync();

    /// <summary>
    /// Создание нового подразделения.
    /// </summary>
    /// <param name="subdivision">Подразделение, которое нужно создать.</param>
    /// <returns>Созданное подразделение.</returns>
    Task<Subdivision> CreateAsync(Subdivision subdivision);

    /// <summary>
    /// Обновление существующего подразделения.
    /// </summary>
    /// <param name="subdivision">Подразделение, которое нужно обновить.</param>
    /// <returns>Обновленное подразделение.</returns>
    Task<Subdivision> UpdateAsync(Subdivision subdivision);

    /// <summary>
    /// Удаление существующего подразделения.
    /// </summary>
    /// <param name="subdivision">Подразделение, которое нужно удалить.</param>
    Task DeleteAsync(Subdivision subdivision);

    /// <summary>
    /// Проверка на существование подразделения с заданным наименованием.
    /// </summary>
    /// <param name="name">Наименование подразделения.</param>
    /// <returns>True, если подразделение с таким наименованием существует, иначе False.</returns>
    Task<bool> ExistsAsync(string name);

    /// <summary>
    /// Поиск подразделений, удовлетворяющих заданной спецификации.
    /// </summary>
    /// <param name="specification">Спецификация для поиска подразделений.</param>
    /// <returns>Подразделение, соответствующее спецификации.</returns>
    Task<Subdivision> FindAsync(ISpecification<Subdivision> specification);
  }
}