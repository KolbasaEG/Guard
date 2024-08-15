using Guard.Domain.Entities;
using Guard.Domain.Exceptions;
using Guard.Infrastructure.Interfaces;

namespace Guard.Business.Services
{
  /// <summary>
  /// Сервис для работы с подразделениями.
  /// </summary>
  public class SubdivisionService
  {
    private readonly ISubdivisionRepository _subdivisionRepository;

    /// <summary>
    /// Конструктор сервиса.
    /// </summary>
    /// <param name="subdivisionRepository">Репозиторий для работы с подразделениями.</param>
    public SubdivisionService(ISubdivisionRepository subdivisionRepository)
    {
      _subdivisionRepository = subdivisionRepository;
    }

    /// <summary>
    /// Получение подразделения по его идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор подразделения.</param>
    /// <returns>Подразделение, соответствующее заданному идентификатору.</returns>
    public async Task<Subdivision> GetSubdivisionByIdAsync(Guid id)
    {
      var subdivision = await _subdivisionRepository.GetByIdAsync(id);

      if (subdivision == null)
      {
        throw new SubdivisionNotFoundException(id);
      }

      return subdivision;
    }

    /// <summary>
    /// Получение всех подразделений.
    /// </summary>
    /// <returns>Коллекция всех подразделений.</returns>
    public async Task<IEnumerable<Subdivision>> GetAllSubdivisionsAsync()
    {
      return await _subdivisionRepository.GetAllAsync();
    }

    /// <summary>
    /// Создание нового подразделения.
    /// </summary>
    /// <param name="subdivision">Подразделение, которое нужно создать.</param>
    /// <returns>Созданное подразделение.</returns>
    public async Task<Subdivision> CreateSubdivisionAsync(Subdivision subdivision)
    {
      // Проверка на существование подразделения с таким же наименованием
      if (await _subdivisionRepository.ExistsAsync(subdivision.Наименование))
      {
        throw new SubdivisionAlreadyExistsException(
            $"Подразделение с наименованием '{subdivision.Наименование}' уже существует.");
      }

      // Создание подразделения
      return await _subdivisionRepository.CreateAsync(subdivision);
    }

    /// <summary>
    /// Обновление существующего подразделения.
    /// </summary>
    /// <param name="subdivision">Подразделение, которое нужно обновить.</param>
    /// <returns>Обновленное подразделение.</returns>
    public async Task<Subdivision> UpdateSubdivisionAsync(Subdivision subdivision)
    {
      return await _subdivisionRepository.UpdateAsync(subdivision);
    }

    /// <summary>
    /// Удаление существующего подразделения.
    /// </summary>
    /// <param name="id">Идентификатор подразделения, которое нужно удалить.</param>
    public async Task DeleteSubdivisionAsync(Guid id)
    {
      var subdivision = await GetSubdivisionByIdAsync(id);
      await _subdivisionRepository.DeleteAsync(subdivision);
    }
  }
}