namespace Guard.Core.Identity;

using Guard.Core.Entities;

public class UserContext
{
  public string UserId { get; set; } = string.Empty;
  public string UserName { get; set; } = string.Empty;

  /// <summary>
  /// Признак суперпользователя / Администратора Root
  /// </summary>
  public bool IsRoot { get; set; }

  /// <summary>Аккаунт пользователя</summary>
  public ApplicationUser? User { get; set; }

  /// <summary>Связанная физ. лицо / сотрудник (для Root может быть null)</summary>
  public Personal? Personal { get; set; }

  /// <summary>Текущее подразделение пользователя (для Root может быть null)</summary>
  public Subdivision? Subdivision { get; set; }

  /// <summary>
  /// Подчиненные подразделения (для Root — все подразделения системы)
  /// </summary>
  public List<Subdivision> SubordinateSubdivisions { get; set; } = new();

  /// <summary>
  /// Множество ID всех доступных подразделений
  /// </summary>
  public HashSet<Guid> AccessibleSubdivisionIds { get; set; } = new();

  /// <summary>
  /// Проверка прав доступа к подразделению
  /// </summary>
  public bool HasAccessToSubdivision(Guid subdivisionId)
  {
    // Пользователь Root имеет доступ к абсолютно любым подразделениям
    if (IsRoot)
      return true;

    return AccessibleSubdivisionIds.Contains(subdivisionId);
  }
}