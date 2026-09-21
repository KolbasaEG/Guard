namespace Guard.Core.Enums;

public enum SubdivisionHierarchyMode
{
  /// <summary>
  /// Только выбранное подразделение
  /// </summary>
  CurrentOnly,

  /// <summary>
  /// Выбранное подразделение и все его подчиненные (дочерние узлы любой глубины)
  /// </summary>
  IncludeChildren
}