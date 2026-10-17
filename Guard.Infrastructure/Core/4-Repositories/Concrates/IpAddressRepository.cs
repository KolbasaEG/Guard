using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Repositories;

/// <summary>
/// Реализация репозитория IP-адресов.
/// </summary>
public class IpAddressRepository : GenericRepository<IpAddress>, IIpAddressRepository
{
  public IpAddressRepository(ApplicationDbContext context) : base(context)
  {
  }

}