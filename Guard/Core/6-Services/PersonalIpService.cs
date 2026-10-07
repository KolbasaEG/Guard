using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public class PersonalIpService(IDbContextFactory<ApplicationDbContext> factory,
    IIpManagementAccessService access, ILogger<PersonalIpService> logger) : IPersonalIpService
{
  private static void RequireAccess(Personal personal, IpManagementScope scope)
  {
    if (!scope.IsRoot && (!personal.SubdivisionId.HasValue || !scope.SubdivisionIds.Contains(personal.SubdivisionId.Value)))
      throw new UnauthorizedAccessException("Сотрудник находится вне доступных подразделений.");
  }

  public async Task<PersonalIpAssignmentsDto> GetAsync(Guid personalId, CancellationToken ct = default)
  {
    var scope = await access.GetScopeAsync(true, ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    var personal = await db.Personals.AsNoTracking().Include(p => p.IpAddresses)
        .SingleOrDefaultAsync(p => p.Id == personalId, ct) ?? throw new KeyNotFoundException("Сотрудник не найден.");
    RequireAccess(personal, scope);
    var options = await db.IpAddresses.AsNoTracking()
        .Where(ip => personal.SubdivisionId.HasValue && ip.SubdivisionId == personal.SubdivisionId &&
            (ip.Status == Status.Inserted || ip.Status == Status.Modified))
        .OrderBy(ip => ip.Address).Select(ip => new IpOptionDto(ip.Id, ip.Address)).ToListAsync(ct);
    // Выбор ограничен подразделением сотрудника, включая операции Root.
    var availableIds = options.Select(ip => ip.Id).ToHashSet();
    return new(options, personal.IpAddresses.Where(ip => availableIds.Contains(ip.Id)).Select(ip => ip.Id).ToList(),
        personal.IpAddresses.Count(ip => ip.SubdivisionId != personal.SubdivisionId || !personal.SubdivisionId.HasValue));
  }

  public async Task UpdateAsync(Guid personalId, IEnumerable<Guid> ipIds, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(ipIds);
    var scope = await access.GetScopeAsync(true, ct);
    var ids = ipIds.Distinct().ToArray();
    await using var db = await factory.CreateDbContextAsync(ct);
    await using var transaction = await db.Database.BeginTransactionAsync(ct);
    await IpAddressService.LockCatalogAsync(db, ct);
    // Сериализуем изменения назначений одного сотрудника.
    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Personals\" WHERE \"Id\" = {personalId} FOR UPDATE", ct);
    var personal = await db.Personals.Include(p => p.IpAddresses).SingleOrDefaultAsync(p => p.Id == personalId, ct)
        ?? throw new KeyNotFoundException("Сотрудник не найден.");
    RequireAccess(personal, scope);
    if (personal.Status is not (Status.Inserted or Status.Modified))
      throw new InvalidOperationException("Назначения можно менять только у активного незаблокированного сотрудника.");
    var selected = await db.IpAddresses.Where(ip => ids.Contains(ip.Id)).ToListAsync(ct);
    if (selected.Count != ids.Length || selected.Any(ip => ip.Status is not (Status.Inserted or Status.Modified) ||
        !personal.SubdivisionId.HasValue || ip.SubdivisionId != personal.SubdivisionId))
      throw new InvalidOperationException("Выбраны недоступные или неактивные IP-адреса. Обновите список.");
    foreach (var old in personal.IpAddresses.ToList())
      if (personal.SubdivisionId.HasValue && old.SubdivisionId == personal.SubdivisionId)
        personal.IpAddresses.Remove(old);
    foreach (var ip in selected) personal.IpAddresses.Add(ip);
    // Фабрика не подключает scoped-интерцептор: метаданные операции задаются явно.
    personal.ModifiedBy = scope.UserId;
    personal.LastModifiedDate = DateTime.UtcNow;
    personal.Status = Status.Modified;
    await db.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);
    logger.LogInformation("Обновлены назначения IP сотрудника {PersonalId} пользователем {UserId}", personalId, scope.UserId);
  }
}
