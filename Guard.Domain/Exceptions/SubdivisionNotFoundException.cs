namespace Guard.Domain.Exceptions
{
  /// <summary>
  /// Исключение, возникающее при попытке получить подразделение, 
  /// которого нет в системе.
  /// </summary>
  public class SubdivisionNotFoundException : Exception
  {
    /// <summary>
    /// Конструктор исключения.
    /// </summary>
    /// <param name="id">Идентификатор отсутствующего подразделения.</param>
    public SubdivisionNotFoundException(Guid id) : base($"Подразделение с идентификатором {id} не найдено.")
    {
    }
  }
}