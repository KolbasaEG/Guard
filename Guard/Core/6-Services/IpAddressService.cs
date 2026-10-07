using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Guard.Core.Services;

public class IpAddressService(IDbContextFactory<ApplicationDbContext> factory,
    IIpManagementAccessService access, ILogger<IpAddressService> logger) : IIpAddressService
{
  private static IQueryable<IpAddress> Accessible(IQueryable<IpAddress> q, IpManagementScope scope) =>
      scope.IsRoot ? q : q.Where(ip => ip.SubdivisionId.HasValue && scope.SubdivisionIds.Contains(ip.SubdivisionId.Value));

  public async Task<TResult> QueryIpAddressesAsync<TResult>(Func<IQueryable<IpAddress>, Task<TResult>> query, CancellationToken ct = default)
  {
    var scope = await access.GetScopeAsync(false, ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    return await query(Accessible(db.IpAddresses.AsNoTracking(), scope));
  }

  public Task<IpAddress?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
      QueryIpAddressesAsync(q => q.SingleOrDefaultAsync(ip => ip.Id == id, ct), ct);

  public async Task<IReadOnlyList<IpAddress>> GetAllActiveAsync(CancellationToken ct = default) =>
      await QueryIpAddressesAsync(q => q.Where(ip => ip.Status == Status.Inserted || ip.Status == Status.Modified)
          .OrderBy(ip => ip.Address).ToListAsync(ct), ct);

  public async Task<IReadOnlyList<IpAddress>> GetBySubdivisionIdAsync(Guid subdivisionId, CancellationToken ct = default) =>
      await QueryIpAddressesAsync(q => q.Where(ip => ip.SubdivisionId == subdivisionId &&
          (ip.Status == Status.Inserted || ip.Status == Status.Modified)).OrderBy(ip => ip.Address).ToListAsync(ct), ct);

  public async Task<bool> IsIpUniqueAsync(string address, Guid? excludeId = null, CancellationToken ct = default)
  {
    await access.GetScopeAsync(true, ct);
    if (!IpAddressRule.TryParse(address, out var rule)) return false;
    await using var db = await factory.CreateDbContextAsync(ct);
    return await IsUniqueAsync(db, rule!.CanonicalAddress, excludeId, ct);
  }

  private static async Task<bool> IsUniqueAsync(ApplicationDbContext db, string address, Guid? excludeId, CancellationToken ct)
  {
    var values = await db.IpAddresses.AsNoTracking().Where(ip => !excludeId.HasValue || ip.Id != excludeId.Value)
        .Select(ip => ip.Address).ToListAsync(ct);
    return !values.Any(value => IpAddressRule.TryParse(value, out var rule) && rule!.CanonicalAddress == address);
  }

  public Task<Guid> CreateAsync(IpAddress ipAddress, CancellationToken ct = default) =>
      WriteAsync(null, ipAddress, null, ct);
  public async Task UpdateAsync(IpAddress ipAddress, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(ipAddress);
    await WriteAsync(ipAddress.Id, ipAddress, null, ct);
  }
  public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default) => await WriteAsync(id, null, "delete", ct);
  public async Task ArchiveAsync(Guid id, CancellationToken ct = default) => await WriteAsync(id, null, "archive", ct);
  public async Task RestoreAsync(Guid id, CancellationToken ct = default) => await WriteAsync(id, null, "restore", ct);
  public async Task BlockAsync(Guid id, CancellationToken ct = default) => await WriteAsync(id, null, "block", ct);
  public async Task UnblockAsync(Guid id, CancellationToken ct = default) => await WriteAsync(id, null, "unblock", ct);

  private async Task<Guid> WriteAsync(Guid? id, IpAddress? input, string? operation, CancellationToken ct)
  {
    var scope = await access.GetScopeAsync(true, ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    await using var transaction = await db.Database.BeginTransactionAsync(ct);
    await LockCatalogAsync(db, ct);
    var entity = id.HasValue ? await Accessible(db.IpAddresses, scope).SingleOrDefaultAsync(ip => ip.Id == id.Value, ct)
        ?? throw new KeyNotFoundException("IP-адрес не найден или недоступен.") : new IpAddress();
    if (operation != null)
      entity.Status = IpStatusTransitions.Apply(entity.Status, operation);
    else
    {
      ArgumentNullException.ThrowIfNull(input);
      if (id.HasValue && entity.Status is not (Status.Inserted or Status.Modified))
        throw new InvalidOperationException("Редактировать можно только активный незаблокированный IP-адрес.");
      if (!scope.IsRoot && (!input.SubdivisionId.HasValue || !scope.SubdivisionIds.Contains(input.SubdivisionId.Value)))
        throw new UnauthorizedAccessException("Подразделение недоступно. Общими IP-адресами управляет Root.");
      if (input.SubdivisionId.HasValue && !await db.Subdivisions.AnyAsync(s => s.Id == input.SubdivisionId &&
          (s.Status == Status.Inserted || s.Status == Status.Modified), ct))
        throw new InvalidOperationException("Выберите активное подразделение.");
      if (!IpAddressRule.TryParse(input.Address, out var rule))
        throw new ArgumentException("Введите корректный IPv4, IPv6 или CIDR-подсеть.");
      if (input.Description?.Length > 1000) throw new ArgumentException("Описание не должно превышать 1000 символов.");
      if (!await IsUniqueAsync(db, rule!.CanonicalAddress, id, ct))
        throw new ArgumentException("Этот IP-адрес или подсеть уже есть в справочнике.");
      entity.Address = rule.CanonicalAddress;
      entity.Description = input.Description;
      entity.SubdivisionId = input.SubdivisionId;
      entity.Status = id.HasValue ? Status.Modified : Status.Inserted;
    }
    if (id.HasValue)
    {
      entity.ModifiedBy = scope.UserId;
      entity.LastModifiedDate = DateTime.UtcNow;
    }
    else
    {
      entity.CreatedBy = scope.UserId;
      entity.InsertedDate = DateTime.UtcNow;
      db.IpAddresses.Add(entity);
    }
    // Фабрика не подключает scoped-интерцептор: метаданные задаются явно.
    try { await db.SaveChangesAsync(ct); }
    catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
    { throw new ArgumentException("Этот IP-адрес или подсеть уже есть в справочнике.", ex); }
    await transaction.CommitAsync(ct);
    logger.LogInformation("Сохранён IP {IpAddressId}, статус {Status}, пользователь {UserId}", entity.Id, entity.Status, scope.UserId);
    return entity.Id;
  }

  // Сериализуем запись каталога и назначения, включая проверку эквивалентных старых строк.
  internal static Task<int> LockCatalogAsync(ApplicationDbContext db, CancellationToken ct) =>
      db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(74162001)", ct);
}
