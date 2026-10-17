namespace Guard.Core.Services;
public record DataAccessScope(string UserId, bool IsRoot, HashSet<Guid> SubdivisionIds)
{
  public bool Allows(Guid? id) => IsRoot || (id.HasValue && SubdivisionIds.Contains(id.Value));
}
public interface IDataAccessScopeService
{
  Task<DataAccessScope> GetAsync(CancellationToken ct = default);
}
