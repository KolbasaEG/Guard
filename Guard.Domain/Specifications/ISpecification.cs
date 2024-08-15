namespace Guard.Domain.Specifications
{
  /// <summary>
  /// Базовый интерфейс для всех спецификаций.
  /// </summary>
  /// <typeparam name="T">Тип объекта, для которого определена спецификация.</typeparam>
  public interface ISpecification<T>
  {
    /// <summary>
    /// Проверяет, удовлетворяет ли объект заданной спецификации.
    /// </summary>
    /// <param name="entity">Объект, для которого выполняется проверка.</param>
    /// <returns>True, если объект соответствует спецификации, иначе False.</returns>
    bool IsSatisfiedBy(T entity);
  }
}