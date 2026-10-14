using Guard.Core.Contexts;
using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public interface IDirectoryManagementService
{
  Task<bool> IsApiManagedAsync(CancellationToken ct = default);
}

public sealed class DirectoryManagementService(IDbContextFactory<ApplicationDbContext> factory) : IDirectoryManagementService
{
  public async Task<bool> IsApiManagedAsync(CancellationToken ct = default)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    return await db.SynchronizationSettings.AnyAsync(s => s.Id == 1 && s.Mode == DataManagementMode.Api, ct);
  }

  internal static async Task RequireLocalAsync(IUnitOfWork work, CancellationToken ct)
  {
    if (await work.BasicRepository<SynchronizationSettings>().Query().AnyAsync(s => s.Id == 1 && s.Mode == DataManagementMode.Api, ct))
      throw new EntityRuleException("Данными управляет API. Доступен только просмотр.");
  }
}
