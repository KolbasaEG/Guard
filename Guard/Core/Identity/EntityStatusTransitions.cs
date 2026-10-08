using Guard.Core.Enums;

namespace Guard.Core.Identity;

public static class EntityStatusTransitions
{
  public static bool CanApply(Status current, StatusOperation operation, bool isRoot = false) =>
    (operation, current) switch
    {
      (StatusOperation.Archive, Status.Inserted or Status.Modified) => true,
      (StatusOperation.Block, Status.Inserted or Status.Modified or Status.Archived) => true,
      (StatusOperation.Unblock, Status.Blocked or Status.ArchivedBlocked) => true,
      (StatusOperation.Delete, Status.Inserted or Status.Modified or Status.Archived) => true,
      (StatusOperation.Restore, Status.Archived) => true,
      (StatusOperation.Restore, Status.Deleted) => isRoot,
      _ => false
    };

  public static Status Apply(Status current, StatusOperation operation, bool isRoot = false)
  {
    if (current == Status.Deleted && operation == StatusOperation.Restore && !isRoot)
      throw new Guard.Core.Services.EntityRuleException("Восстановление удалённой записи доступно только Root.");
    if (!CanApply(current, operation, isRoot))
      throw new Guard.Core.Services.EntityRuleException("Операция недоступна для текущего состояния записи. Обновите данные; заблокированную запись сначала разблокируйте.");
    return operation switch
    {
      StatusOperation.Archive => Status.Archived,
      StatusOperation.Block => current == Status.Archived ? Status.ArchivedBlocked : Status.Blocked,
      StatusOperation.Unblock => current == Status.ArchivedBlocked ? Status.Archived : Status.Modified,
      StatusOperation.Delete => Status.Deleted,
      StatusOperation.Restore => Status.Modified,
      _ => throw new ArgumentOutOfRangeException(nameof(operation))
    };
  }
}
