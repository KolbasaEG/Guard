using Guard.Core.Contexts;
using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public sealed class DirectoryManagementService(IDbContextFactory<ApplicationDbContext> factory, IOperationAvailability? availability = null) : IDirectoryManagementService
{
  public async Task<bool> IsApiManagedAsync(CancellationToken ct = default)
  {
    if (availability != null) await availability.RequireWorkingAsync(ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    var result = await db.SynchronizationSettings.AnyAsync(s => s.Id == 1 && s.Mode == DataManagementMode.Api, ct);
    if (availability != null) await availability.RequireWorkingAsync(ct);
    return result;
  }

  internal static async Task RequireLocalAsync(IUnitOfWork work, CancellationToken ct)
  {
    if (await work.BasicRepository<SynchronizationSettings>().Query().AnyAsync(s => s.Id == 1 && s.Mode == DataManagementMode.Api, ct))
      throw new EntityRuleException("Данными управляет API. Доступен только просмотр.");
  }
}
