namespace Guard.Domain.Exceptions
{
  /// <summary>
  /// Исключение, возникающее при попытке создания подразделения, 
  /// которое уже существует в системе.
  /// </summary>
  public class SubdivisionAlreadyExistsException : Exception
  {
    /// <summary>
    /// Конструктор исключения.
    /// </summary>
    /// <param name="message">Сообщение об ошибке.</param>
    public SubdivisionAlreadyExistsException(string message) : base(message)
    {
    }
  }
}