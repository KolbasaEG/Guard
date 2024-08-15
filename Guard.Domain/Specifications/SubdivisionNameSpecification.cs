using Guard.Domain.Entities;

namespace Guard.Domain.Specifications
{
  /// <summary>
  /// Спецификация для поиска подразделений по наименованию.
  /// </summary>
  public class SubdivisionNameSpecification : ISpecification<Subdivision>
  {
    private readonly string _name;

    /// <summary>
    /// Конструктор спецификации.
    /// </summary>
    /// <param name="name">Наименование подразделения, по которому выполняется поиск.</param>
    public SubdivisionNameSpecification(string name)
    {
      _name = name;
    }

    /// <summary>
    /// Проверяет, удовлетворяет ли подразделение заданной спецификации.
    /// </summary>
    /// <param name="subdivision">Подразделение, для которого выполняется проверка.</param>
    /// <returns>True, если подразделение соответствует спецификации, иначе False.</returns>
    public bool IsSatisfiedBy(Subdivision subdivision)
    {
      return subdivision.Наименование == _name;
    }
  }
}