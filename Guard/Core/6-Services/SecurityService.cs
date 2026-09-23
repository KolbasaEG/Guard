using System.Security.Claims;
using Guard.Core.Entities;
using Guard.Core.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Guard.Core.Services;

public class SecurityService : ISecurityService
{
  private readonly UserManager<ApplicationUser> _userManager;
  private readonly RoleManager<ApplicationRole> _roleManager;
  private readonly IReadRepository<ApplicationUser> _userReadRepository;
  private readonly IReadRepository<ApplicationRole> _roleReadRepository;
  private readonly IReadRepository<Personal> _personalReadRepository;
  private readonly IReadRepository<IpAddress> _ipAddressReadRepository;
  private readonly IReadRepository<Personal> _personalRepository;
  private readonly IReadRepository<IpAddress> _ipAddressRepository;
  private readonly ILogger<SecurityService> _logger;

  public SecurityService(
      UserManager<ApplicationUser> userManager,
      RoleManager<ApplicationRole> roleManager,
      IReadRepository<ApplicationUser> userReadRepository,
      IReadRepository<ApplicationRole> roleReadRepository,
      IReadRepository<Personal> personalReadRepository,
      IReadRepository<IpAddress> ipAddressReadRepository,
      IReadRepository<Personal> personalRepository,
      IReadRepository<IpAddress> ipAddressRepository,
      ILogger<SecurityService> logger)
  {
    _userManager = userManager;
    _roleManager = roleManager;
    _userReadRepository = userReadRepository;
    _roleReadRepository = roleReadRepository;
    _personalReadRepository = personalReadRepository;
    _ipAddressReadRepository = ipAddressReadRepository;
    _personalRepository = personalRepository;
    _ipAddressRepository = ipAddressRepository;
    _logger = logger;
  }

  #region Гибкие методы чтения (Querying)

  public async Task<TResult> QueryUsersAsync<TResult>(
      Func<IQueryable<ApplicationUser>, Task<TResult>> query,
      CancellationToken ct = default)
  {
    return await _userReadRepository.QueryAsync(query, ct);
  }

  public async Task<TResult> QueryRolesAsync<TResult>(
      Func<IQueryable<ApplicationRole>, Task<TResult>> query,
      CancellationToken ct = default)
  {
    return await _roleReadRepository.QueryAsync(query, ct);
  }

  public async Task<TResult> QueryIpAddressesAsync<TResult>(
      Func<IQueryable<IpAddress>, Task<TResult>> query,
      CancellationToken ct = default)
  {
    return await _ipAddressReadRepository.QueryAsync(query, ct);
  }

  public async Task<TResult> QueryPersonalsAsync<TResult>(
      Func<IQueryable<Personal>, Task<TResult>> query,
      CancellationToken ct = default)
  {
    return await _personalReadRepository.QueryAsync(query, ct);
  }

  #endregion

  #region Операции с пользователями

  public async Task<ApplicationUser?> GetUserByIdAsync(string id, CancellationToken ct = default)
  {
    return await QueryUsersAsync(async query =>
        await query
            .AsNoTracking()
            .Include(u => u.Personal)
            .FirstOrDefaultAsync(u => u.Id == id, ct), ct);
  }

  public async Task<IdentityResult> CreateUserAsync(ApplicationUser user, string password, IEnumerable<string>? roles = null, CancellationToken ct = default)
  {
    var result = await _userManager.CreateAsync(user, password);
    if (!result.Succeeded) return result;

    if (roles != null && roles.Any())
    {
      result = await _userManager.AddToRolesAsync(user, roles);
    }

    _logger.LogInformation("Создан пользователь {UserName} (ID: {UserId})", user.UserName, user.Id);
    return result;
  }

  public async Task<IdentityResult> UpdateUserAsync(ApplicationUser user, IEnumerable<string> roles, CancellationToken ct = default)
  {
    var existingUser = await _userManager.FindByIdAsync(user.Id);
    if (existingUser == null)
      return IdentityResult.Failed(new IdentityError { Description = $"Пользователь с ID {user.Id} не найден." });

    existingUser.Email = user.Email;
    existingUser.UserName = user.UserName;
    existingUser.PersonalId = user.PersonalId;

    var result = await _userManager.UpdateAsync(existingUser);
    if (!result.Succeeded) return result;

    var currentRoles = await _userManager.GetRolesAsync(existingUser);
    var rolesToAdd = roles.Except(currentRoles);
    var rolesToRemove = currentRoles.Except(roles);

    await _userManager.AddToRolesAsync(existingUser, rolesToAdd);
    await _userManager.RemoveFromRolesAsync(existingUser, rolesToRemove);

    return IdentityResult.Success;
  }

  public async Task<IdentityResult> ToggleUserLockoutAsync(string userId, bool lockout, CancellationToken ct = default)
  {
    var user = await _userManager.FindByIdAsync(userId);
    if (user == null)
      return IdentityResult.Failed(new IdentityError { Description = $"Пользователь с ID {userId} не найден." });

    var lockoutEnd = lockout ? DateTimeOffset.UtcNow.AddYears(100) : (DateTimeOffset?)null;
    return await _userManager.SetLockoutEndDateAsync(user, lockoutEnd);
  }

  public async Task<IdentityResult> ResetPasswordAsync(string userId, string newPassword, CancellationToken ct = default)
  {
    var user = await _userManager.FindByIdAsync(userId);
    if (user == null)
      return IdentityResult.Failed(new IdentityError { Description = $"Пользователь с ID {userId} не найден." });

    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
    return await _userManager.ResetPasswordAsync(user, token, newPassword);
  }

  #endregion

  #region Роли и Права (Claims)

  public async Task<List<string>> GetUserRolesAsync(string userId, CancellationToken ct = default)
  {
    var user = await _userManager.FindByIdAsync(userId);
    if (user == null) return new();

    var roles = await _userManager.GetRolesAsync(user);
    return roles.ToList();
  }

  public async Task<List<string>> GetRoleClaimsAsync(string roleId, CancellationToken ct = default)
  {
    var role = await _roleManager.FindByIdAsync(roleId);
    if (role == null) return new();

    var claims = await _roleManager.GetClaimsAsync(role);
    return claims.Where(c => c.Type == "Permission").Select(c => c.Value).ToList();
  }

  public async Task<IdentityResult> UpdateRoleClaimsAsync(string roleId, IEnumerable<string> permissions, CancellationToken ct = default)
  {
    var role = await _roleManager.FindByIdAsync(roleId);
    if (role == null)
      return IdentityResult.Failed(new IdentityError { Description = $"Роль с ID {roleId} не найдена." });

    var currentClaims = await _roleManager.GetClaimsAsync(role);
    var currentPermissions = currentClaims.Where(c => c.Type == "Permission").ToList();

    foreach (var claim in currentPermissions)
    {
      await _roleManager.RemoveClaimAsync(role, claim);
    }

    foreach (var perm in permissions)
    {
      await _roleManager.AddClaimAsync(role, new Claim("Permission", perm));
    }

    return IdentityResult.Success;
  }

  public async Task<List<string>> GetUserClaimsAsync(string userId, CancellationToken ct = default)
  {
    var user = await _userManager.FindByIdAsync(userId);
    if (user == null) return new();

    var claims = await _userManager.GetClaimsAsync(user);
    return claims.Where(c => c.Type == "Permission").Select(c => c.Value).ToList();
  }

  public async Task<IdentityResult> UpdateUserClaimsAsync(string userId, IEnumerable<string> permissions, CancellationToken ct = default)
  {
    var user = await _userManager.FindByIdAsync(userId);
    if (user == null)
      return IdentityResult.Failed(new IdentityError { Description = $"Пользователь с ID {userId} не найден." });

    var currentClaims = await _userManager.GetClaimsAsync(user);
    var currentPermissions = currentClaims.Where(c => c.Type == "Permission").ToList();

    foreach (var claim in currentPermissions)
    {
      await _userManager.RemoveClaimAsync(user, claim);
    }

    foreach (var perm in permissions)
    {
      await _userManager.AddClaimAsync(user, new Claim("Permission", perm));
    }

    return IdentityResult.Success;
  }

  #endregion

  #region IP-Адреса и Доступ

  public async Task UpdatePersonalIpAddressesAsync(Guid personalId, IEnumerable<Guid> ipAddressIds, CancellationToken ct = default)
  {
    //var personal = await _personalRepository.QueryAsync(async personals =>
    //    await personals
    //        .Include(p => p.IpAddresses)
    //        .FirstOrDefaultAsync(p => p.Id == personalId, ct), ct);

    //if (personal == null)
    //  throw new KeyNotFoundException($"Сотрудник с ID {personalId} не найден.");

    //var selectedIps = await _ipAddressRepository.QueryAsync(async ips =>
    //    await ips
    //        .Where(i => ipAddressIds.Contains(i.Id))
    //        .ToListAsync(ct), ct);

    //personal.IpAddresses.Clear();
    //foreach (var ip in selectedIps)
    //{
    //  personal.IpAddresses.Add(ip);
    //}

    //await _personalRepository.SaveChangesAsync(ct);
  }

  public async Task<bool> IsIpAllowedForUserAsync(string userId, string clientIp, CancellationToken ct = default)
  {
    return await QueryUsersAsync(async users =>
    {
      var user = await users
          .AsNoTracking()
          .Include(u => u.Personal)
              .ThenInclude(p => p!.IpAddresses)
          .FirstOrDefaultAsync(u => u.Id == userId, ct);

      if (user?.Personal == null || !user.Personal.IpAddresses.Any())
        return true;

      return user.Personal.IpAddresses.Any(i => i.Address == clientIp);
    }, ct);
  }

  #endregion
}