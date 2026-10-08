namespace Guard.Core.Services;

public sealed class EntityConflictException() : InvalidOperationException(
  "Запись уже изменена другим оператором. Сохраните введённые значения и откройте актуальную запись.");
