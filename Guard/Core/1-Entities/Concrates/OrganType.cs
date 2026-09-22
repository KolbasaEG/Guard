namespace Guard.Core.Entities;

/// <summary>
/// Справочник типов органов.
/// </summary>
public class OrganType
{
  /// <summary>
  /// Идентификатор типа органа.
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// Тип классификатора (константа 906 для типов органов).
  /// </summary>
  public int ClassifierType { get; set; } = 906;

  /// <summary>
  /// Код внутри классификатора.
  /// </summary>
  public int Code { get; set; }

  /// <summary>
  /// Наименование типа органа.
  /// </summary>
  public string Name { get; set; } = string.Empty;

  // --- Navigation ---

  /// <summary>
  /// Ссылка на элемент универсального классификатора по парам (ClassifierType, Code).
  /// </summary>
  public Classifier? Classifier { get; set; }

  public ICollection<Subdivision> Subdivisions { get; private set; } = new List<Subdivision>();

}