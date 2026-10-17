namespace Guard.Core.Services;

/// <summary>Материализованные данные для серверной области подразделений.</summary>
public interface IDataAccessScopeReader
{
  Task<string?> FindUserPathAsync(string userId, CancellationToken ct = default);
  Task<IReadOnlyList<Guid>> GetSubdivisionIdsAsync(string path, CancellationToken ct = default);
}
