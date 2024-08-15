using Guard.Business.Services;
using Guard.Domain.Entities;

namespace Guard.Business.UseCases
{
  /// <summary>
  /// Use Case для получения подразделения по идентификатору.
  /// </summary>
  public class GetSubdivisionByIdUseCase
  {
    private readonly SubdivisionService _subdivisionService;

    /// <summary>
    /// Конструктор Use Case.
    /// </summary>
    /// <param name="subdivisionService">Сервис для работы с подразделениями.</param>
    public GetSubdivisionByIdUseCase(SubdivisionService subdivisionService)
    {
      _subdivisionService = subdivisionService;
    }

    /// <summary>
    /// Выполнение Use Case.
    /// </summary>
    /// <param name="id">Идентификатор подразделения.</param>
    /// <returns>Подразделение, соответствующее заданному идентификатору.</returns>
    public async Task<Subdivision> Execute(Guid id)
    {
      return await _subdivisionService.GetSubdivisionByIdAsync(id);
    }
  }
}