using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public class IpAddressService : IIpAddressService
{
  private readonly IReadRepository<IpAddress> _readIpAddressRepository;
  private readonly IUnitOfWork _uow;
  private readonly ILogger<IpAddressService> _logger;

  public IpAddressService(
      IReadRepository<IpAddress> readIpAddressRepository,
      IUnitOfWork unitOfWork,
      ILogger<IpAddressService> logger)
  {
    _readIpAddressRepository = readIpAddressRepository;
    _uow = unitOfWork;
    _logger = logger;
  }

  // ==================== Read (Изолированный IReadRepository) ====================

  public async Task<TResult> QueryIpAddressesAsync<TResult>(
      Func<IQueryable<IpAddress>, Task<TResult>> query,
      CancellationToken ct = default)
  {
    return await _readIpAddressRepository.QueryAsync(query, ct);
  }

  public async Task<IpAddress?> GetByIdAsync(Guid id, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос IP-адреса по ID: {IpAddressId}", id);
    return await _readIpAddressRepository.GetByIdAsync(id, ct);
  }

  public async Task<IReadOnlyList<IpAddress>> GetAllActiveAsync(CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос всех активных IP-адресов");
    return await _readIpAddressRepository.QueryAsync(query =>
        query.Where(ip => ip.Status <= Status.Archived)
             .OrderBy(ip => ip.Address)
             .ToListAsync(ct),
        ct);
  }

  public async Task<IReadOnlyList<IpAddress>> GetBySubdivisionIdAsync(Guid subdivisionId, CancellationToken ct = default)
  {
    _logger.LogDebug("Запрос IP-адресов для подразделения ID: {SubdivisionId}", subdivisionId);
    return await _readIpAddressRepository.QueryAsync(query =>
        query.Where(ip => ip.SubdivisionId == subdivisionId && ip.Status <= Status.Archived)
             .OrderBy(ip => ip.Address)
             .ToListAsync(ct),
        ct);
  }

  public async Task<bool> IsIpUniqueAsync(string address, Guid? excludeId = null, CancellationToken ct = default)
  {
    if (string.IsNullOrWhiteSpace(address))
      return true;

    var normalizedAddress = address.Trim();

    return await _readIpAddressRepository.QueryAsync(query =>
        query.AllAsync(ip => ip.Address != normalizedAddress || (excludeId.HasValue && ip.Id == excludeId.Value), ct),
        ct);
  }

  // ==================== Write (IUnitOfWork & ChangeTracker) ====================

  public async Task<Guid> CreateAsync(IpAddress ipAddress, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(ipAddress);

    return await _uow.ExecuteInTransactionAsync(async () =>
    {
      ipAddress.Status = Status.Inserted;

      // Очищаем адрес от лишних пробелов
      if (!string.IsNullOrWhiteSpace(ipAddress.Address))
      {
        ipAddress.Address = ipAddress.Address.Trim();
      }

      await _uow.BaseEntityRepository<IpAddress>().AddAsync(ipAddress, ct);
      await _uow.SaveChangesAsync(ct);

      _logger.LogInformation("Создана новая запись IP-адреса '{IpAddress}' (ID: {IpAddressId})",
          ipAddress.Address, ipAddress.Id);

      return ipAddress.Id;
    }, ct);
  }

  public async Task UpdateAsync(IpAddress ipAddress, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(ipAddress);

    if (!string.IsNullOrWhiteSpace(ipAddress.Address))
    {
      ipAddress.Address = ipAddress.Address.Trim();
    }

    await _uow.BaseEntityRepository<IpAddress>().UpdateAsync(ipAddress, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("Обновлены данные IP-адреса '{IpAddress}' (ID: {IpAddressId})",
        ipAddress.Address, ipAddress.Id);
  }

  // ==================== Status Management ====================

  public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
  {
    var ipAddress = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<IpAddress>().SoftDeleteAsync(ipAddress, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("IP-адрес '{IpAddress}' (ID: {IpAddressId}) помечен как удаленный",
        ipAddress.Address, id);
  }

  public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
  {
    var ipAddress = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<IpAddress>().ArchiveAsync(ipAddress, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("IP-адрес '{IpAddress}' (ID: {IpAddressId}) отправлен в архив",
        ipAddress.Address, id);
  }

  public async Task BlockAsync(Guid id, CancellationToken ct = default)
  {
    var ipAddress = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<IpAddress>().BlockAsync(ipAddress, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogWarning("IP-адрес '{IpAddress}' (ID: {IpAddressId}) заблокирован",
        ipAddress.Address, id);
  }

  public async Task UnblockAsync(Guid id, CancellationToken ct = default)
  {
    var ipAddress = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<IpAddress>().UnblockAsync(ipAddress, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("IP-адрес '{IpAddress}' (ID: {IpAddressId}) разблокирован",
        ipAddress.Address, id);
  }

  public async Task RestoreAsync(Guid id, CancellationToken ct = default)
  {
    var ipAddress = await GetRequiredForWriteAsync(id, ct);

    await _uow.BaseEntityRepository<IpAddress>().RestoreAsync(ipAddress, ct);
    await _uow.SaveChangesAsync(ct);

    _logger.LogInformation("IP-адрес '{IpAddress}' (ID: {IpAddressId}) восстановлен",
        ipAddress.Address, id);
  }

  // ==================== Private Helpers ====================

  private async Task<IpAddress> GetRequiredForWriteAsync(Guid id, CancellationToken ct)
  {
    var ipAddress = await _uow.BaseEntityRepository<IpAddress>().GetByIdAsync(id, ct);

    if (ipAddress == null)
    {
      _logger.LogWarning("Попытка выполнения операции над несуществующим IP-адресом (ID: {IpAddressId})", id);
      throw new KeyNotFoundException($"IP-адрес с ID '{id}' не найден.");
    }

    return ipAddress;
  }
}