using Guard.Core.Entities;

namespace Guard.Core.Services;

/// <summary>
/// Сервис для управления ролями пользователей (ApplicationRole).
/// </summary>
public interface IApplicationRoleService
{
  // ==================== Read Operations ====================

  /// <summary>
  /// Выполнение произвольного LINQ-запроса над ролями (Read-only context).
  /// </summary>
  Task<TResult> QueryRolesAsync<TResult>(
      Func<IQueryable<ApplicationRole>, Task<TResult>> query,
      CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает роль по её уникальному строковому идентификатору.
  /// </summary>
  /// <param name="id">Идентификатор роли (Guid/String)</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Экземпляр <see cref="ApplicationRole"/> или <c>null</c>, если роль не найдена.</returns>
  Task<ApplicationRole?> GetByIdAsync(string id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает роль по её наименованию.
  /// </summary>
  /// <param name="roleName">Имя роли</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Экземпляр <see cref="ApplicationRole"/> или <c>null</c>, если роль не найдена.</returns>
  Task<ApplicationRole?> GetByNameAsync(string roleName, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает список всех ролей.
  /// </summary>
  /// <param name="ct">Токен отмены операции</param>
  Task<IReadOnlyList<ApplicationRole>> GetAllAsync(CancellationToken ct = default);

  /// <summary>
  /// Проверяет, уникально ли имя роли в базе данных.
  /// </summary>
  /// <param name="roleName">Наименование роли</param>
  /// <param name="excludeId">Исключаемый ID (при редактировании существующей роли)</param>
  /// <param name="ct">Токен отмены операции</param>
  Task<bool> IsRoleNameUniqueAsync(string roleName, string? excludeId = null, CancellationToken ct = default);

  // ==================== Write Operations ====================

  /// <summary>
  /// Асинхронно создает новую роль.
  /// </summary>
  /// <param name="role">Заполненная модель роли</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Уникальный идентификатор созданной роли.</returns>
  Task<string> CreateAsync(ApplicationRole role, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно обновляет данные существующей роли.
  /// </summary>
  /// <param name="role">Модель роли с обновлёнными данными</param>
  /// <param name="ct">Токен отмены операции</param>
  Task UpdateAsync(ApplicationRole role, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно выполняет физическое удаление роли.
  /// </summary>
  /// <param name="id">Идентификатор роли</param>
  /// <param name="ct">Токен отмены операции</param>
  Task DeleteAsync(string id, CancellationToken ct = default);
}