using Guard.Core.Entities;
using Guard.Core.Services.DTOs;
using Guard.Core.Enums;
using Guard.Core.Logging;

namespace Guard.Core.Services;

public partial class SecurityService
{
  public Task<string> CreateFromDtoAsync(CreateUserCommand input, CancellationToken ct = default) =>
    AuditOperation.RunAsync(audit, AuditEventType.UserCreated, "Account", null, () => CreateFromDtoCoreAsync(input, ct));
  private async Task<string> CreateFromDtoCoreAsync(CreateUserCommand input, CancellationToken ct)
  {
    ArgumentNullException.ThrowIfNull(input);
    ValidateUserFields(input.UserName, input.Email);
    if (input.PersonalId == Guid.Empty) throw new ArgumentException("Выберите сотрудника.");
    if (string.IsNullOrEmpty(input.Password)) throw new ArgumentException("Укажите пароль.");
    var user = new ApplicationUser { UserName = input.UserName.Trim(), Email = input.Email.Trim(), PersonalId = input.PersonalId };
    Check(await CreateUserAsync(user, input.Password, ct: ct));
    return user.Id;
  }

  public Task SaveDetailsAsync(EditUserDto input, CancellationToken ct = default) =>
    AuditOperation.RunAsync(audit, AuditEventType.UserDetailsChanged, "Account", input?.Id, () => SaveDetailsCoreAsync(input!, ct));
  private async Task SaveDetailsCoreAsync(EditUserDto input, CancellationToken ct)
  {
    ArgumentNullException.ThrowIfNull(input);
    // Форма может изменить модель, пока прикладная операция ожидает доступ/БД.
    var snapshot = new EditUserDto { Id = input.Id, Version = input.Version, UserName = input.UserName,
      Email = input.Email, PersonalId = input.PersonalId };
    Check(await UpdateUserDetailsAsync(snapshot, ct));
  }

  public Task ResetPasswordFromDtoAsync(ResetUserPasswordCommand input, CancellationToken ct = default) =>
    AuditOperation.RunAsync(audit, AuditEventType.PasswordChangeFailed, "Account", input?.UserId, () => ResetPasswordCoreAsync(input!, ct));
  private async Task ResetPasswordCoreAsync(ResetUserPasswordCommand input, CancellationToken ct)
  {
    ArgumentNullException.ThrowIfNull(input);
    if (string.IsNullOrWhiteSpace(input.UserId)) throw new ArgumentException("Не выбран пользователь.");
    if (string.IsNullOrEmpty(input.Password)) throw new ArgumentException("Укажите пароль.");
    Check(await ResetPasswordAsync(input.UserId, input.Password, ct));
  }

  private static void ValidateUserFields(string? userName, string? email)
  {
    if (string.IsNullOrWhiteSpace(userName) || userName.Trim().Length > 256 ||
        string.IsNullOrWhiteSpace(email) || email.Trim().Length > 256 ||
        !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email.Trim()))
      throw new ArgumentException("Укажите имя пользователя и корректный email (не более 256 символов).");
  }

  private async Task<(UserAccessSnapshot Actor, DataAccessScope Scope)> RequireUserWriteAsync(CancellationToken ct)
  {
    ct.ThrowIfCancellationRequested();
    await permissions.RequireAsync(Guard.Core.Identity.Permissions.Users.Manage, ct);
    var actor = await permissions.GetCurrentAsync(ct);
    if (!actor.Has(Guard.Core.Identity.Permissions.Users.Manage)) throw new UnauthorizedAccessException();
    var access = await accessScopes.GetAsync(ct);
    if (actor.UserId != access.UserId || actor.IsRoot != access.IsRoot) throw new UnauthorizedAccessException("Доступ изменился.");
    return (actor with { Permissions = new(actor.Permissions) }, access with { SubdivisionIds = new(access.SubdivisionIds) });
  }

  private async Task RecheckUserWriteAsync((UserAccessSnapshot Actor, DataAccessScope Scope) initial, CancellationToken ct)
  {
    var current = await RequireUserWriteAsync(ct);
    if (initial.Actor.UserId != current.Actor.UserId || initial.Actor.IsRoot != current.Actor.IsRoot ||
        !initial.Actor.Permissions.SetEquals(current.Actor.Permissions) ||
        !initial.Scope.SubdivisionIds.SetEquals(current.Scope.SubdivisionIds))
      throw new UnauthorizedAccessException("Доступ изменился. Откройте форму заново.");
  }
}
