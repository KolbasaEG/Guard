using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

internal static class SearchAccess
{
  public static async Task<UserAccessSnapshot> RequireAsync(IPermissionService permissions, string permission, CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();
    await permissions.RequireAsync(permission, ct);
    var actor = await permissions.GetCurrentAsync(ct);
    if (!actor.Has(permission)) throw new UnauthorizedAccessException();
    return actor with { Permissions = new(actor.Permissions) };
  }

  public static async Task RecheckAsync(IPermissionService permissions, string permission, UserAccessSnapshot initial, CancellationToken ct)
  {
    var current = await RequireAsync(permissions, permission, ct);
    if (initial.UserId != current.UserId || initial.IsRoot != current.IsRoot || !initial.Permissions.SetEquals(current.Permissions))
      throw new UnauthorizedAccessException("Доступ изменился. Повторите загрузку.");
    ct.ThrowIfCancellationRequested();
  }
}
