using Guard.Core.Contexts;
using Guard.Core.Interceptors;
using Guard.Core.Services;
using Microsoft.EntityFrameworkCore;

public sealed class UnitOfWorkFactory(IDbContextFactory<ApplicationDbContext> contexts,
    IPermissionService permissions, IServiceProvider services, ILogger<UnitOfWork> logger) : IUnitOfWorkFactory
{
  public async Task<IUnitOfWork> CreateAsync(CancellationToken ct = default)
  {
    var actor = await permissions.GetCurrentAsync(ct);
    var db = await contexts.CreateDbContextAsync(ct);
    db.SavingChanges += (_, _) => AuditSaveChangesInterceptor.ApplyAudit(db, actor.UserId);
    return new UnitOfWork(db, services, logger);
  }
}
