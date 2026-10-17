namespace Guard.Core.Services.Exports;

public sealed record ExportDeliveryFile(string FileName, string ContentType, long Size);

public interface IExportDeliveryService
{
  /// <summary>
  /// Проверяет доступ и атомарно занимает одноразовую передачу. Callback должен завершить
  /// копирование и сброс буфера до возврата; исключение или отмена означают неуспешную попытку.
  /// Поток принадлежит сервису и действителен только во время callback.
  /// После занятия задания результат записывается даже при отмене вызывающей стороны.
  /// </summary>
  Task DeliverAsync(Guid id, Func<ExportDeliveryFile, Stream, CancellationToken, Task> deliver,
    CancellationToken ct = default);
}
