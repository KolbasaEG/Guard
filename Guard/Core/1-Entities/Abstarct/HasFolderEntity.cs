using Guard.Core.Enums;

namespace Guard.Core.Entities;

public abstract class HasFolderEntity
{
  /// <summary>
  /// Каталог с файлами
  /// </summary>
  public string? Folder { get; set; }

}
