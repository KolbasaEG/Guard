using System;

namespace Guard.Domain.Events
{
  /// <summary>
  /// Событие, возникающее при создании нового подразделения.
  /// </summary>
  public class SubdivisionCreated : IEvent
  {
    /// <summary>
    /// Идентификатор созданного подразделения.
    /// </summary>
    public Guid SubdivisionId { get; }

    /// <summary>
    /// Конструктор для создания события SubdivisionCreated.
    /// </summary>
    /// <param name="subdivisionId">Идентификатор созданного подразделения.</param>
    public SubdivisionCreated(Guid subdivisionId)
    {
      SubdivisionId = subdivisionId;
    }
  }
}