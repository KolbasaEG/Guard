namespace Guard.Core.Interceptors;

using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
  private readonly ICurrentSession _session;

  public AuditSaveChangesInterceptor(ICurrentSession session)
  {
    _session = session;
  }

  public override InterceptionResult<int> SavingChanges(
      DbContextEventData eventData,
      InterceptionResult<int> result)
  {
    var pending = _session.GetAsync();
    if (!pending.IsCompleted)
      throw new InvalidOperationException("Для асинхронного контекста сеанса используйте SaveChangesAsync.");
    ApplyAudit(eventData.Context, pending.GetAwaiter().GetResult()?.UserId ?? "system");
    return base.SavingChanges(eventData, result);
  }

  public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
      DbContextEventData eventData,
      InterceptionResult<int> result,
      CancellationToken cancellationToken = default)
  {
    var actor = await _session.GetAsync(cancellationToken);
    ApplyAudit(eventData.Context, actor?.UserId ?? "system");
    return await base.SavingChangesAsync(eventData, result, cancellationToken);
  }

  internal static void ApplyAudit(DbContext? context, string userId)
  {
    if (context is null) return;
    var now = DateTime.UtcNow;

    foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
    {
      switch (entry.State)
      {
        case EntityState.Added:
          entry.Entity.CreatedBy = userId;
          entry.Entity.InsertedDate = now;
          entry.Entity.Status = Status.Inserted;
          entry.Entity.ModifiedBy = null;
          entry.Entity.LastModifiedDate = null;
          break;

        case EntityState.Modified:
          entry.Entity.ModifiedBy = userId;
          entry.Entity.LastModifiedDate = now;

          entry.Property(x => x.CreatedBy).IsModified = false;
          entry.Property(x => x.InsertedDate).IsModified = false;

          if (entry.Entity.Status == Status.Inserted)
            entry.Entity.Status = Status.Modified;
          break;
      }
    }
  }
}
