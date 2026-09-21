namespace Guard.Core.Enums
{
  /// <summary>
  /// «Универсальный справочник» (Universal Lookup / Single Classifier Table)
  /// Чтобы избавиться от «магических чисел» в коде, тип классификатора Type представлен в виде enum ClassifierType
  /// </summary>
  public enum ClassifierType
  {
    DocumentType = 1,
    EquipmentStatus = 2,
    TaskPriority = 3
  }
}
