using Guard.Core.Enums;

namespace Guard.Core.Entities;

/// <summary>
/// Базовый класс для всех сущностей доменной модели Guard.
/// 
/// Назначение:
/// - Единая точка для аудита (кто и когда создал/изменил).
/// - Управление жизненным циклом через Status (soft workflow вместо жёсткого удаления).
/// - Использование UUIDv7 для естественной сортировки по времени создания.
/// 
/// Важно про типы ключей:
/// - Доменные сущности (Subdivision, Asset и т.д.) используют Guid (UUIDv7).
/// - ASP.NET Core Identity (ApplicationUser / ApplicationRole) использует string в качестве Id.
/// - Поэтому поля CreatedBy и ModifiedBy имеют тип string, чтобы корректно ссылаться на Identity пользователя.
/// 
/// Почему именно так:
/// - Guid.CreateVersion7() даёт отличную производительность вставки в PostgreSQL.
/// - Status enum позволяет реализовать сложные бизнес-процессы (архивация, блокировка и т.д.).
/// - Все изменения состояния происходят только через доменные методы (инкапсуляция).
/// </summary>
public abstract class BaseEntity
{
  /// <summary>
  /// Уникальный идентификатор записи. Генерируется как UUIDv7.
  /// </summary>
  public Guid Id { get; set; } = Guid.CreateVersion7();

  /// <summary>
  /// Текущий статус жизненного цикла сущности.
  /// </summary>
  public Status Status { get; set; } = Status.Inserted;

  /// <summary>
  /// Дата и время создания записи (UTC).
  /// </summary>
  public DateTime InsertedDate { get; set; }

  /// <summary>
  /// Дата и время последнего изменения записи (UTC).
  /// </summary>
  public DateTime? LastModifiedDate { get; set; }

  /// <summary>
  /// Идентификатор пользователя (из Identity), создавшего запись.
  /// Тип string, потому что ApplicationUser.Id имеет тип string.
  /// </summary>
  public string CreatedBy { get; set; } = string.Empty;

  /// <summary>
  /// Идентификатор пользователя (из Identity), последним изменившего запись.
  /// Тип string, потому что ApplicationUser.Id имеет тип string.
  /// </summary>
  public string? ModifiedBy { get; set; }

}
