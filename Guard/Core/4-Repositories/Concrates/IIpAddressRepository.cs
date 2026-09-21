using Guard.Core.Entities;
using Guard.Core.Enums;

namespace Guard.Core.Repositories;

/// <summary>
/// Репозиторий для работы со справочником IP-адресов.
/// </summary>
public interface IIpAddressRepository : IGenericRepository<IpAddress>
{
}