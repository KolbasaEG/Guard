namespace Guard.Core.Enums
{
  public enum MaintenanceTaskStatus
  {
    Planned = 0,     // Запланировано
    InProgress = 1,  // В процессе
    Completed = 2,   // Завершено
    Canceled = 3,    // Отменено
    Failed = 4       // Сорвано / Перенесено
  }
}
