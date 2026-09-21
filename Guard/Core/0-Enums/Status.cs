namespace Guard.Core.Enums
{
  public enum Status
  {
    Inserted = 1, //Добавлено
    Modified = 2, //Изменено
    Blocked = 3, //Заблокировано для редактирования
    Archived = 4, //Помещено в архив
    ArchivedBlocked = 5, //Помещено в архив, заблокировано для редактирования
    Deleted = 6  //Удалено
  }
}
