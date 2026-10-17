using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

/// <summary>
/// Сервис для управления IP-адресами (IpAddress).
/// </summary>
public interface IIpAddressService : IIpAddressManagementService
{
  // ==================== Read Operations ====================

  /// <summary>
  /// Выполнение произвольного LINQ-запроса над IP-адресами (Read-only context).
  /// </summary>
  Task<TResult> QueryIpAddressesAsync<TResult>(Func<IQueryable<IpAddress>, Task<TResult>> query, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает IP-адрес по его уникальному идентификатору.
  /// </summary>
  /// <param name="id">Уникальный идентификатор IP-адреса (Guid)</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Экземпляр <see cref="IpAddress"/> или <c>null</c>, если запись не найдена.</returns>
  Task<IpAddress?> GetByIdAsync(Guid id, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает список всех активных IP-адресов (исключая удалённые и архивированные).
  /// </summary>
  /// <param name="ct">Токен отмены операции</param>
  Task<IReadOnlyList<IpAddress>> GetAllActiveAsync(CancellationToken ct = default);

  /// <summary>
  /// Асинхронно получает список IP-адресов, привязанных к конкретному подразделению.
  /// </summary>
  /// <param name="subdivisionId">Идентификатор подразделения</param>
  /// <param name="ct">Токен отмены операции</param>
  Task<IReadOnlyList<IpAddress>> GetBySubdivisionIdAsync(Guid subdivisionId, CancellationToken ct = default);

  /// <summary>
  /// Проверяет, уникален ли IP-адрес в базе данных.
  /// </summary>
  /// <param name="address">Строковое представление IP-адреса</param>
  /// <param name="excludeId">Исключаемый ID (при редактировании существующей записи)</param>
  /// <param name="ct">Токен отмены операции</param>
  Task<bool> IsIpUniqueAsync(string address, Guid? excludeId = null, CancellationToken ct = default);

  // ==================== Write Operations ====================

  /// <summary>
  /// Асинхронно создает новую запись IP-адреса.
  /// </summary>
  /// <param name="ipAddress">Заполненная модель IP-адреса</param>
  /// <param name="ct">Токен отмены операции</param>
  /// <returns>Уникальный идентификатор созданной записи (UUIDv7).</returns>
  Task<Guid> CreateAsync(IpAddress ipAddress, CancellationToken ct = default);

  /// <summary>
  /// Асинхронно обновляет данные существующего IP-адреса.
  /// </summary>
  /// <param name="ipAddress">Модель IP-адреса с обновлёнными данными</param>
  /// <param name="ct">Токен отмены операции</param>
  Task UpdateAsync(IpAddress ipAddress, CancellationToken ct = default);
}
