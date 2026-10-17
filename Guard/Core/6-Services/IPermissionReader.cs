using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

/// <summary>Данные хранения для проверки сессии и ролевых полномочий.</summary>
public interface IPermissionReader
{
  Task<string?> GetSecurityStampAsync(string userId, CancellationToken ct = default);
  Task<PermissionAccessData?> FindAccessAsync(string userId, CancellationToken ct = default);
}
