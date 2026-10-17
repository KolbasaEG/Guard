using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Guard.Core.Services;

public class IpAddressService(IDbContextFactory<ApplicationDbContext> factory,
    IIpManagementAccessService access, ILogger<IpAddressService> logger) : IIpAddressManagementService
{
  public async Task<IpAddressDetailsDto?> GetDetailsAsync(Guid id, CancellationToken ct = default)
  {
    var scope = await access.GetScopeAsync(false, ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    var result = await Accessible(db.IpAddresses.AsNoTracking(), scope).Where(ip => ip.Id == id)
      .Select(ip => new IpAddressDetailsDto {
        Id = ip.Id, Address = ip.Address, Description = ip.Description, SubdivisionId = ip.SubdivisionId,
        SubdivisionName = ip.Subdivision == null ? null : ip.Subdivision.Name, Status = ip.Status,
        Info = new EntityInfoDto {
          Id = ip.Id, InsertedDate = ip.InsertedDate, LastModifiedDate = ip.LastModifiedDate,
          Creator = db.Users.Where(u => u.Id == ip.CreatedBy).Select(u => u.Email).FirstOrDefault(),
          Editor = db.Users.Where(u => u.Id == ip.ModifiedBy).Select(u => u.Email).FirstOrDefault()
        }
      }).SingleOrDefaultAsync(ct);
    await EnsureReadScopeAsync(scope, ct);
    return result;
  }

  public async Task<IReadOnlyList<IpSubdivisionOptionDto>> GetSubdivisionOptionsAsync(CancellationToken ct = default)
  {
    var scope = await access.GetScopeAsync(false, ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    var result = await db.Subdivisions.AsNoTracking().Where(s =>
      (s.Status == Status.Inserted || s.Status == Status.Modified) && (scope.IsRoot || scope.SubdivisionIds.Contains(s.Id)))
      .OrderBy(s => s.Name).ThenBy(s => s.Id).Select(s => new IpSubdivisionOptionDto(s.Id, s.Name)).ToListAsync(ct);
    await EnsureReadScopeAsync(scope, ct);
    return result;
  }

  private async Task EnsureReadScopeAsync(IpManagementScope initial, CancellationToken ct)
  {
    var final = await access.GetScopeAsync(false, ct);
    ct.ThrowIfCancellationRequested();
    if (initial.UserId != final.UserId || initial.IsRoot != final.IsRoot || !initial.SubdivisionIds.SetEquals(final.SubdivisionIds))
      throw new UnauthorizedAccessException("Область доступа изменилась во время загрузки.");
  }

  public async Task<Guard.Core.Services.DTOs.IpSearchResult> SearchAsync(Guard.Core.Services.DTOs.IpSearchRequest request, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    if (request.Skip < 0 || request.Take is < 1 or > 100) throw new ArgumentException("Недопустимые параметры страницы.");
    IpSearchQuery.ValidateFilter(request.Filter);
    var initial = await access.GetScopeAsync(false, ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    var ownPath = await db.Users.Where(u => u.Id == initial.UserId)
      .Select(u => u.Personal == null || u.Personal.Subdivision == null ? null : u.Personal.Subdivision.Path).SingleOrDefaultAsync(ct);
    var query = IpSearchQuery.ApplyVisibility(Accessible(db.IpAddresses.AsNoTracking(), initial), request.Mode,
      request.AllSubdivisions, initial.IsRoot, ownPath, request.Hierarchy);
    query = IpSearchQuery.Filter(query, request.Filter);
    var sorted = IpSearchQuery.Sort(query, request.OrderBy);
    var total = await query.CountAsync(ct);
    var items = await sorted.Skip(request.Skip).Take(request.Take).Select(p => new Guard.Core.Services.DTOs.IpAddressDto {
      Id = p.Id, Address = p.Address, Description = p.Description, Status = (int)p.Status,
      SubdivisionName = p.Subdivision == null ? null : p.Subdivision.Name
    }).ToListAsync(ct);
    var final = await access.GetScopeAsync(false, ct);
    var finalPath = await db.Users.Where(u => u.Id == final.UserId)
      .Select(u => u.Personal == null || u.Personal.Subdivision == null ? null : u.Personal.Subdivision.Path).SingleOrDefaultAsync(ct);
    ct.ThrowIfCancellationRequested();
    if (initial.UserId != final.UserId || initial.IsRoot != final.IsRoot ||
        !initial.SubdivisionIds.SetEquals(final.SubdivisionIds) || ownPath != finalPath)
      throw new UnauthorizedAccessException("Область доступа изменилась во время загрузки.");
    return new(items, total);
  }

  private static IQueryable<IpAddress> Accessible(IQueryable<IpAddress> q, IpManagementScope scope) =>
      scope.IsRoot ? q : q.Where(ip => ip.SubdivisionId.HasValue && scope.SubdivisionIds.Contains(ip.SubdivisionId.Value));

  private static async Task<bool> IsUniqueAsync(ApplicationDbContext db, string address, Guid? excludeId, CancellationToken ct)
  {
    if (await db.IpAddresses.AnyAsync(ip => EF.Property<string>(ip, IpCatalogKeys.Canonical) == null, ct))
      throw new EntityRuleException("Каталог IP проходит подготовку. Обратитесь к администратору и повторите изменение позже.");
    return !await db.IpAddresses.AnyAsync(ip => EF.Property<string>(ip, IpCatalogKeys.Canonical) == address &&
      (!excludeId.HasValue || ip.Id != excludeId.Value), ct);
  }

  public Task<Guid> CreateAsync(CreateIpAddressDto input, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(input);
    return WriteAsync(null, new(input.Address, input.Description, input.SubdivisionId), null, ct);
  }
  public async Task UpdateAsync(EditIpAddressDto input, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(input);
    await WriteAsync(input.Id, new(input.Address, input.Description, input.SubdivisionId), null, ct);
  }
  public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default) => await WriteAsync(id, null, "delete", ct);
  public async Task ArchiveAsync(Guid id, CancellationToken ct = default) => await WriteAsync(id, null, "archive", ct);
  public async Task RestoreAsync(Guid id, CancellationToken ct = default) => await WriteAsync(id, null, "restore", ct);
  public async Task BlockAsync(Guid id, CancellationToken ct = default) => await WriteAsync(id, null, "block", ct);
  public async Task UnblockAsync(Guid id, CancellationToken ct = default) => await WriteAsync(id, null, "unblock", ct);

  private async Task<Guid> WriteAsync(Guid? id, CreateIpAddressDto? input, string? operation, CancellationToken ct)
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
        throw new EntityRuleException("Редактировать можно только активный незаблокированный IP-адрес.");
      if (!scope.IsRoot && (!input.SubdivisionId.HasValue || !scope.SubdivisionIds.Contains(input.SubdivisionId.Value)))
        throw new UnauthorizedAccessException("Подразделение недоступно. Общими IP-адресами управляет Root.");
      if (input.SubdivisionId.HasValue && !await db.Subdivisions.AnyAsync(s => s.Id == input.SubdivisionId &&
          (s.Status == Status.Inserted || s.Status == Status.Modified), ct))
        throw new EntityRuleException("Выберите активное подразделение.");
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
