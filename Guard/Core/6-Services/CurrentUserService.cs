using Guard.Core.Entities;
using Guard.Core.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Guard.Core.Services;

public class CurrentUserService : ICurrentUserService
{
  private readonly IHttpContextAccessor _httpContextAccessor;
  private readonly IReadRepository<ApplicationUser> _userRepository;
  private readonly IReadRepository<Subdivision> _subdivisionRepository;
  private readonly ILogger<CurrentUserService> _logger;

  private UserContext? _cachedContext;
  private readonly SemaphoreSlim _semaphore = new(1, 1);

  public CurrentUserService(
      IHttpContextAccessor httpContextAccessor,
      IReadRepository<ApplicationUser> userRepository,
      IReadRepository<Subdivision> subdivisionRepository,
      ILogger<CurrentUserService> logger)
  {
    _httpContextAccessor = httpContextAccessor;
    _userRepository = userRepository;
    _subdivisionRepository = subdivisionRepository;
    _logger = logger;
  }

  public string? UserId =>
      _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

  public string? UserName =>
      _httpContextAccessor.HttpContext?.User?.Identity?.Name;

  public async Task<UserContext?> GetContextAsync(CancellationToken ct = default)
  {
    if (_cachedContext != null)
      return _cachedContext;

    await _semaphore.WaitAsync(ct);
    try
    {
      if (_cachedContext != null)
        return _cachedContext;

      _cachedContext = await LoadUserContextInternalAsync(ct);
      return _cachedContext;
    }
    finally
    {
      _semaphore.Release();
    }
  }

  public void RefreshContext()
  {
    _cachedContext = null;
  }

  private async Task<UserContext?> LoadUserContextInternalAsync(CancellationToken ct)
  {
    var userId = UserId;
    if (string.IsNullOrEmpty(userId))
      return null;

    try
    {
      var principal = _httpContextAccessor.HttpContext?.User;

      // Проверка наличия роли Root (или ClaimTypes.Role)
      var isRoot = principal?.IsInRole("Root") ?? false;

      // 1. Загрузка аккаунта пользователя со связанным Personal и Subdivision
      var user = await _userRepository.QueryAsync(async query =>
          await query
              .Include(u => u.Personal)
                  .ThenInclude(p => p!.Subdivision)
              .FirstOrDefaultAsync(u => u.Id == userId, ct),
          ct);

      if (user == null)
        return null;

      var currentSubdivision = user.Personal?.Subdivision;
      IReadOnlyList<Subdivision> subordinates = Array.Empty<Subdivision>();
      var accessibleIds = new HashSet<Guid>();

      // 2. Формирование списка доступных подразделений
      if (isRoot)
      {
        // Для Root загружаем все активные подразделения системы
        subordinates = await _subdivisionRepository.QueryAsync(async query =>
            await query
                .Where(s => s.Status < Enums.Status.Archived)
                .OrderBy(p => p.Name)
                .ToListAsync(ct),
            ct);

        foreach (var sub in subordinates)
        {
          accessibleIds.Add(sub.Id);
        }
      }
      else if (currentSubdivision != null)
      {
        // Для обычного пользователя — только свое подразделение и подчиненные по Path
        if (!string.IsNullOrEmpty(currentSubdivision.Path))
        {
          subordinates = await _subdivisionRepository.QueryAsync(async query =>
              await query
                  .Where(s => s.Status < Enums.Status.Archived && s.Path.StartsWith(currentSubdivision.Path))
                  .OrderBy(p=>p.Name)
                  .ToListAsync(ct),
              ct);
        }

        accessibleIds.Add(currentSubdivision.Id);
        foreach (var sub in subordinates)
        {
          accessibleIds.Add(sub.Id);
        }
      }

      return new UserContext
      {
        UserId = userId,
        UserName = UserName ?? "Undefined",
        IsRoot = isRoot,
        User = user,
        Personal = user.Personal,
        Subdivision = currentSubdivision,
        SubordinateSubdivisions = subordinates.ToList(),
        AccessibleSubdivisionIds = accessibleIds
      };
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Ошибка при загрузке контекста пользователя {UserId}", userId);
      return null;
    }
  }
}