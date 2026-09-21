namespace Guard.Core.Entities;

public abstract class HasArchiveBatchIdEntity
{
  /// <summary>
  /// Идентификатор пакета архива
  /// </summary>
  public Guid? ArchiveBatchId { get; set; }

}
