using Guard.Core.Identity;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public partial class SecurityService
{
  private async Task<(UserAccessSnapshot Actor, DataAccessScope Scope)> RequireUserReadAsync(CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();
    var actor = await permissions.GetCurrentAsync(ct);
    if (!actor.Has(Permissions.Users.Read)) throw new UnauthorizedAccessException();
    var scope = await accessScopes.GetAsync(ct);
    if (actor.UserId != scope.UserId || actor.IsRoot != scope.IsRoot) throw new UnauthorizedAccessException("Доступ изменился. Повторите загрузку.");
    return (actor with { Permissions = new(actor.Permissions) }, scope with { SubdivisionIds = new(scope.SubdivisionIds) });
  }

  public async Task<UserSearchResult> SearchUsersAsync(UserSearchRequest request, CancellationToken ct = default)
  {
    ArgumentNullException.ThrowIfNull(request);
    var access = await RequireUserReadAsync(ct);
    if (request.Skip < 0 || request.Take is < 1 or > 100) throw new ArgumentException("Недопустимые параметры страницы.");
    UserSearchQuery.ValidateFilter(request.Filter);
    var policy = await policies.GetPasswordLifetimeAsync(ct);
    var now = DateTimeOffset.UtcNow;
    var cutoff = now.AddDays(-policy.PasswordDays);
    var result = await userReads.QueryAsync(async query => {
      // Повторное сужение на снимок области: репозиторий также проверяет текущий доступ.
      if (!access.Scope.IsRoot) query = query.Where(u => u.Personal != null && u.Personal.SubdivisionId.HasValue &&
        access.Scope.SubdivisionIds.Contains(u.Personal.SubdivisionId.Value));
      query = UserSearchQuery.Filter(query, request.Filter);
      var count = await query.CountAsync(ct);
      var items = await UserSearchQuery.Sort(query, request.OrderBy).Skip(request.Skip).Take(request.Take)
        .Select(u => new UserDto {
          Id = u.Id, Email = u.Email ?? "-", UserName = u.UserName ?? "-", PersonalId = u.PersonalId,
          PersonalFullName = u.Personal != null ? u.Personal.FullName : "-",
          IsLockedOut = u.AccountBlockReason != null || u.LockoutEnabled && u.LockoutEnd > now,
          AccountBlockReason = u.AccountBlockReason, LastActivityAtUtc = u.LastActivityAtUtc,
          PasswordChangedAtUtc = u.PasswordChangedAtUtc, MustChangePassword = u.MustChangePassword,
          PasswordChangeRequired = u.MustChangePassword || policy.PasswordExpirationEnabled &&
            (u.PasswordChangedAtUtc != null ? u.PasswordChangedAtUtc <= cutoff :
              u.CreatedAtUtc <= cutoff && (policy.PasswordEnabledAtUtc == null || policy.PasswordEnabledAtUtc <= cutoff))
        }).ToListAsync(ct);
      return new UserSearchResult(items, count);
    }, ct);
    var current = await RequireUserReadAsync(ct);
    if (access.Actor.UserId != current.Actor.UserId || access.Actor.IsRoot != current.Actor.IsRoot ||
        !access.Actor.Permissions.SetEquals(current.Actor.Permissions) ||
        !access.Scope.SubdivisionIds.SetEquals(current.Scope.SubdivisionIds))
      throw new UnauthorizedAccessException("Доступ изменился. Повторите загрузку.");
    ct.ThrowIfCancellationRequested();
    return result;
  }
}
