using Guard.Core.Enums;

namespace Guard.Core.Identity;

public static class IpStatusTransitions
{
  public static Status Apply(Status current, string operation) => (operation, current) switch
  {
    ("delete", Status.Inserted or Status.Modified or Status.Archived) => Status.Deleted,
    ("archive", Status.Inserted or Status.Modified) => Status.Archived,
    ("restore", Status.Archived or Status.Deleted) => Status.Modified,
    ("block", Status.Inserted or Status.Modified) => Status.Blocked,
    ("block", Status.Archived) => Status.ArchivedBlocked,
    ("unblock", Status.Blocked) => Status.Modified,
    ("unblock", Status.ArchivedBlocked) => Status.Archived,
    _ => throw new InvalidOperationException("Недопустимый переход статуса IP-адреса.")
  };
}
