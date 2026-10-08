namespace Guard.Core.Services;

public static class UserOperationErrors
{
  public static string Message(Exception error) => error switch {
    EntityConflictException => error.Message,
    EntityRuleException => error.Message,
    ArgumentNullException => "Не получены данные формы. Откройте её заново.",
    ArgumentException when error.InnerException == null => error.Message,
    ArgumentException => "Выбранные значения уже используются или связанная запись недоступна. Обновите данные.",
    UnauthorizedAccessException => "Недостаточно прав или область доступа изменилась. Обновите страницу.",
    KeyNotFoundException => "Запись не найдена или недоступна. Обновите список.",
    _ => "Не удалось выполнить операцию. Повторите попытку; если ошибка сохраняется, обратитесь к администратору."
  };
}
