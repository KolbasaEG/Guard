namespace Guard.Core.Services;

public interface IDirectoryManagementService
{
  Task<bool> IsApiManagedAsync(CancellationToken ct = default);
}
