using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Radzen;

namespace Guard.Core.Services;

/// <summary>
/// Сервис управления пользователями для администратора.
/// Включает CRUD пользователей, назначение ролей и IP-адресов.
/// </summary>
public class UserManagementService : IUserManagementService
{
  private readonly UserManager<ApplicationUser> _userManager;
  private readonly RoleManager<ApplicationRole> _roleManager;
  private readonly IUnitOfWork _uow;
  private readonly ApplicationDbContext _context;
  private readonly IReadRepository<IpAddress> _ipReadRepo;
  private readonly IReadRepository<UserIpAddress> _userIpReadRepo;

  public UserManagementService(
      UserManager<ApplicationUser> userManager,
      RoleManager<ApplicationRole> roleManager,
      IUnitOfWork uow,
      ApplicationDbContext context,
      IReadRepository<IpAddress> ipReadRepo,
      IReadRepository<UserIpAddress> userIpReadRepo)
  {
    _userManager = userManager;
    _roleManager = roleManager;
    _uow = uow;
    _context = context;
    _ipReadRepo = ipReadRepo;
    _userIpReadRepo = userIpReadRepo;
  }

  // ==================== ПОЛЬЗОВАТЕЛИ ====================

  public async Task<List<ApplicationUser>> GetAllUsersAsync()
  {
    return await _userManager.Users.ToListAsync();
  }
  public IQueryable<ApplicationUser> GetAllUsersAsQueryable()
  {
    return _context.Users.AsQueryable();
  }
  public async Task<ApplicationUser?> GetUserByIdAsync(string userId)
  {
    return await _userManager.FindByIdAsync(userId);
  }
  public async Task<IdentityResult> CreateUserAsync(ApplicationUser user, string password)
  {
    return await _userManager.CreateAsync(user, password);
  }
  public async Task<IdentityResult> UpdateUserAsync(ApplicationUser user)
  {
    return await _userManager.UpdateAsync(user);
  }
  public async Task<IdentityResult> DeleteUserAsync(string userId)
  {
    var user = await _userManager.FindByIdAsync(userId);
    if (user == null) return IdentityResult.Failed(new IdentityError { Description = "Пользователь не найден" });

    return await _userManager.DeleteAsync(user);
  }

  // ==================== РОЛИ ====================
  public async Task<IdentityResult> CreateRoleAsync(string roleName)
  {
    if (string.IsNullOrWhiteSpace(roleName))
      return IdentityResult.Failed(new IdentityError { Description = "Имя роли не может быть пустым" });

    if (await _roleManager.RoleExistsAsync(roleName))
      return IdentityResult.Failed(new IdentityError { Description = "Роль с таким именем уже существует" });

    var role = new ApplicationRole { Name = roleName };
    return await _roleManager.CreateAsync(role);
  }
  public async Task<IdentityResult> UpdateRoleAsync(ApplicationRole role)
  {
    if (role == null)
      return IdentityResult.Failed(new IdentityError { Description = "Роль не передана" });

    return await _roleManager.UpdateAsync(role);
  }
  public async Task<IdentityResult> DeleteRoleAsync(string roleName)
  {
    if (string.IsNullOrWhiteSpace(roleName))
      return IdentityResult.Failed(new IdentityError { Description = "Имя роли не может быть пустым" });

    var role = await _roleManager.FindByNameAsync(roleName);
    if (role == null)
      return IdentityResult.Failed(new IdentityError { Description = "Роль не найдена" });

    // Проверяем, что роль не используется пользователями (опционально)
    var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
    if (usersInRole.Count > 0)
      return IdentityResult.Failed(new IdentityError { Description = "Роль используется пользователями. Сначала снимите роль у всех пользователей." });

    return await _roleManager.DeleteAsync(role);
  }
  public IQueryable<ApplicationRole> GetAllRolesAsQueryable()
  {
    return _context.Roles.AsQueryable();
  }
  public async Task<List<ApplicationRole>> GetAllRolesAsync()
  {
    return await _roleManager.Roles.ToListAsync();
  }
  public async Task<IList<string>> GetUserRolesAsync(string userId)
  {
    var user = await _userManager.FindByIdAsync(userId);
    if (user == null) return new List<string>();

    return await _userManager.GetRolesAsync(user);
  }
  public async Task<IdentityResult> AssignRoleToUserAsync(string userId, string roleName)
  {
    var user = await _userManager.FindByIdAsync(userId);
    if (user == null) return IdentityResult.Failed(new IdentityError { Description = "Пользователь не найден" });

    if (!await _roleManager.RoleExistsAsync(roleName))
      return IdentityResult.Failed(new IdentityError { Description = "Роль не существует" });

    return await _userManager.AddToRoleAsync(user, roleName);
  }
  public async Task<IdentityResult> RemoveRoleFromUserAsync(string userId, string roleName)
  {
    var user = await _userManager.FindByIdAsync(userId);
    if (user == null) return IdentityResult.Failed(new IdentityError { Description = "Пользователь не найден" });

    return await _userManager.RemoveFromRoleAsync(user, roleName);
  }


  // ==================== IP-АДРЕСА (справочник) ====================
  public async Task<(List<IpAddress> Items, int TotalCount)> GetIpAddressesAsync(
    int skip,
    int top,
    string? orderBy = null,
    IEnumerable<CompositeFilterDescriptor>? filters = null,
    Status? status = null,                   
    CancellationToken ct = default)
  {
    List<IpAddress> Items = [];
    return (Items, 0);
    //  await _ipReadRepo.QueryAsync(async query =>
    //{
    //  // Фильтрация по статусу
    //  if (status.HasValue)
    //  {
    //    query = query.Where(x => x.Status == status.Value);
    //  }
    //  else
    //  {
    //    // По умолчанию не показываем удалённые
    //    query = query.Where(x => x.Status != Status.Deleted);
    //  }

    //  // Фильтры от Radzen
    //  query = query.ApplyRadzenFilter(filters);

    //  // Сортировка
    //  query = query.ApplyOrdering(orderBy);

    //  var totalCount = await query.CountAsync(ct);

    //  var items = await query
    //      .Skip(skip)
    //      .Take(top)
    //      .ToListAsync(ct);

    //  return (items, totalCount);
    //}, ct);
  }

  public async Task<IpAddress?> GetIpAddressByIdAsync(Guid id, CancellationToken ct = default)
  {
    var ip = await _ipReadRepo.GetByIdAsync(id, ct);
    return ip is null || ip.Status == Status.Deleted ? null : ip;
  }

  public async Task<IpAddress> CreateIpAddressAsync(
      string address,
      string name,
      string? description = null,
      CancellationToken ct = default)
  {
    return await _uow.ExecuteInTransactionAsync(async () =>
    {
      var repo = _uow.Repository<IpAddress>();

      //if (await repo.ExistsByAddressAsync(address, ct))
      //  throw new InvalidOperationException($"IP-адрес '{address}' уже существует.");

      var ip = new IpAddress
      {
        Address = address.Trim(),
        Name = name.Trim(),
        Description = description
      };

      await repo.AddAsync(ip, ct);
      return ip;
    }, ct);
  }

  public async Task UpdateIpAddressAsync(IpAddress ipAddress, CancellationToken ct = default)
  {
    await _uow.ExecuteInTransactionAsync(async () =>
    {
      var repo = _uow.Repository<IpAddress>();
      var existing = await repo.GetByIdAsync(ipAddress.Id, ct)
          ?? throw new InvalidOperationException("IP-адрес не найден.");

      existing.Address = ipAddress.Address.Trim();
      existing.Name = ipAddress.Name.Trim();
      existing.Description = ipAddress.Description;

      repo.Update(existing);
    }, ct);
  }
  public async Task SoftDeleteIpAddressAsync(Guid id, CancellationToken ct = default)
  {
    await _uow.ExecuteInTransactionAsync(async () =>
    {
      var repo = _uow.Repository<IpAddress>();
      var ip = await repo.GetByIdAsync(id, ct)
          ?? throw new InvalidOperationException("IP-адрес не найден.");

      repo.SoftDelete(ip);
    }, ct);
  }
  public async Task ArchiveIpAddressAsync(Guid id, CancellationToken ct = default)
  {
    await _uow.ExecuteInTransactionAsync(async () =>
    {
      var repo = _uow.Repository<IpAddress>();
      var ip = await repo.GetByIdAsync(id, ct)
          ?? throw new InvalidOperationException("IP-адрес не найден.");

      repo.Archive(ip);
    }, ct);
  }
  public async Task RestoreIpAddressAsync(Guid id, CancellationToken ct = default)
  {
    await _uow.ExecuteInTransactionAsync(async () =>
    {
      var repo = _uow.Repository<IpAddress>();
      var ip = await repo.GetByIdAsync(id, ct)
          ?? throw new InvalidOperationException("IP-адрес не найден.");

      repo.Restore(ip);
    }, ct);
  }






  public async Task<List<IpAddress>> GetAllIpAddressesAsync(CancellationToken ct = default)
  {
    var items = await _ipReadRepo.GetAllAsync(ct);
    return items.Where(x => x.Status != Status.Deleted).ToList();
  }



  public async Task DeleteIpAddressAsync(Guid id, string modifiedBy, CancellationToken ct = default)
  {
    await _uow.ExecuteInTransactionAsync(async () =>
    {
      var repo = _uow.Repository<IpAddress>();
      var ip = await repo.GetByIdAsync(id, ct)
          ?? throw new InvalidOperationException("IP-адрес не найден.");

      // Soft-delete через Status
      ip.Status = Status.Deleted;
      ip.ModifiedBy = modifiedBy;
      ip.LastModifiedDate = DateTime.UtcNow;

      repo.Update(ip);
    }, ct);
  }

  // ==================== НАЗНАЧЕНИЕ IP ПОЛЬЗОВАТЕЛЮ ====================

  public async Task AssignIpToUserAsync(
      string userId,
      Guid ipAddressId,
      string assignedBy,
      string? purpose = "Login",
      string? description = null,
      CancellationToken ct = default)
  {
    await _uow.ExecuteInTransactionAsync(async () =>
    {
      // Проверяем пользователя
      var user = await _userManager.FindByIdAsync(userId)
          ?? throw new InvalidOperationException("Пользователь не найден.");

      var ipRepo = _uow.Repository<IpAddress>();
      var assignmentRepo = _uow.Repository<UserIpAddress>();

      var ip = await ipRepo.GetByIdAsync(ipAddressId, ct)
          ?? throw new InvalidOperationException("IP-адрес не найден.");

      if (ip.Status == Status.Deleted || ip.Status == Status.Archived)
        throw new InvalidOperationException("Нельзя назначить удалённый или архивный IP-адрес.");

      // Проверяем, нет ли уже активного назначения
      var alreadyExists = await assignmentRepo.Query()
          .AnyAsync(x => x.UserId == userId && x.IpAddressId == ipAddressId, ct);

      if (alreadyExists)
        throw new InvalidOperationException("Этот IP уже назначен пользователю.");

      var assignment = new UserIpAddress(userId, ipAddressId, assignedBy, purpose, description);
      await assignmentRepo.AddAsync(assignment, ct);
    }, ct);
  }

  public async Task RemoveIpFromUserAsync(string userId, Guid ipAddressId, CancellationToken ct = default)
  {
    await _uow.ExecuteInTransactionAsync(async () =>
    {
      var assignmentRepo = _uow.Repository<UserIpAddress>();

      var assignment = await assignmentRepo.Query()
          .FirstOrDefaultAsync(x => x.UserId == userId && x.IpAddressId == ipAddressId, ct);

      if (assignment is null)
        throw new InvalidOperationException("Назначение IP не найдено.");

      assignmentRepo.Delete(assignment);
    }, ct);
  }

  public async Task<(List<IpAddress> Items, int TotalCount)> GetIpAddressesAsync(int skip, int top, string? orderBy = null, IEnumerable<CompositeFilterDescriptor>? filters = null, CancellationToken ct = default)
  {
    List<IpAddress> Items = [];
    return (Items, 0);
    //  await _ipReadRepo.QueryAsync(async query =>
    //{
    //  // 1. Базовый фильтр
    //  query = query.Where(x => x.Status != Status.Deleted);

    //  // 3. Сортировка
    //  if (!string.IsNullOrEmpty(orderBy))
    //  {
    //    query = query.OrderBy(orderBy);
    //  }

    //  // 4. Подсчёт и пагинация
    //  var totalCount = await query.CountAsync(ct);

    //  var items = await query
    //      .Skip(skip)
    //      .Take(top)
    //      .ToListAsync(ct);

    //  return (items, totalCount);
    //}, ct);
  }
}