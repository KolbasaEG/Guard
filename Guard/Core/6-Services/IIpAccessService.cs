namespace Guard.Core.Services;

public interface IIpAccessService
{
  Task<bool> IsAllowedAsync(string userId, string? clientIp, CancellationToken ct = default);
}
