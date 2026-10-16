namespace Guard.Core.Services;

public interface IPersonalStatusService
{
  Task SoftDeleteAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
  Task ArchiveAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
  Task BlockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
  Task UnblockAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
  Task RestoreAsync(Guid id, CancellationToken ct = default, Guid? expectedVersion = null);
}
