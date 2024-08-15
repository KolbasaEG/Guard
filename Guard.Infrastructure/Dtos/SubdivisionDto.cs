namespace Guard.Infrastructure.Dtos
{
  /// <summary>
  /// DTO (Data Transfer Object) для модели Subdivision.
  /// </summary>
  public class SubdivisionDto
  {
    /// <summary>
    /// Идентификатор подразделения.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Корневое подразделение.
    /// </summary>
    public string Корневой { get; set; }

    /// <summary>
    /// Региональное подразделение.
    /// </summary>
    public string Региональный { get; set; }

    /// <summary>
    /// Территориальное подразделение.
    /// </summary>
    public string Территориальный { get; set; }

    /// <summary>
    /// Субтерриториальное подразделение.
    /// </summary>
    public string Субтерриториальный { get; set; }

    /// <summary>
    /// Адрес подразделения.
    /// </summary>
    public string Адрес { get; set; }

    /// <summary>
    /// Уровень подразделения.
    /// </summary>
    public int Уровень { get; set; }

    /// <summary>
    /// Наименование подразделения.
    /// </summary>
    public string Наименование { get; set; }
  }
}