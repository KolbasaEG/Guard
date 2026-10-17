namespace Guard.Core.Services;

public record IpManagementScope(string UserId, bool IsRoot, HashSet<Guid> SubdivisionIds);

public interface IIpManagementAccessService
{
  Task<IpManagementScope> GetScopeAsync(bool write, CancellationToken ct = default);
}
