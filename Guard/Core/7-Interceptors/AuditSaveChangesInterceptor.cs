namespace Guard.Core.Interceptors;

using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
  private readonly ICurrentUserService _currentUserService;

  public AuditSaveChangesInterceptor(ICurrentUserService currentUserService)
  {
    _currentUserService = currentUserService;
  }

  public override InterceptionResult<int> SavingChanges(
      DbContextEventData eventData,
      InterceptionResult<int> result)
  {
    ApplyAudit(eventData.Context);
    return base.SavingChanges(eventData, result);
  }

  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
      DbContextEventData eventData,
      InterceptionResult<int> result,
      CancellationToken cancellationToken = default)
  {
    ApplyAudit(eventData.Context);
    return base.SavingChangesAsync(eventData, result, cancellationToken);
  }

  private void ApplyAudit(DbContext? context)
  {
    if (context is null) return;

    var userId = _currentUserService.UserId ?? "system";
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