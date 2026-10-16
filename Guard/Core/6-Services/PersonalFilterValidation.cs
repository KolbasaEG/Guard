using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

/// <summary>Проверка независимого контракта фильтра, общая для Web, поиска и экспорта.</summary>
public static class PersonalFilterValidation
{
  public static void Validate(PersonalFilter? filter)
  {
    if (filter == null) return;
    var count = 0;
    ValidateNode(filter, 0, ref count);
  }

  private static void ValidateNode(PersonalFilter filter, int depth, ref int count)
  {
    if (depth > 16 || ++count > 100) throw new ArgumentException("Слишком сложный фильтр.");
    if (filter == null) throw new ArgumentException("Некорректное условие фильтра сотрудников.");
    if (filter.Children != null)
    {
      foreach (var child in filter.Children) ValidateNode(child, depth + 1, ref count);
      return;
    }
    if (filter.Field is not ("LastName" or "FirstName" or "MiddleName" or "FullName" or "Subdivision.Name"))
      throw new ArgumentException("Недопустимое поле фильтра сотрудников.");
    if (filter.Operation is not ("IsNull" or "IsNotNull" or "IsEmpty" or "IsNotEmpty" or
      "Equals" or "NotEquals" or "Contains" or "DoesNotContain" or "StartsWith" or "EndsWith"))
      throw new ArgumentException("Недопустимая операция фильтра сотрудников.");
  }
}
