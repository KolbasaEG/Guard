using Guard.Core.Enums;
using Guard.Core.Identity;

namespace Guard.Core.Services;

public class IpAccessService(IIpAccessReader reader) : IIpAccessService
{
  public async Task<bool> IsAllowedAsync(string userId, string? clientIp, CancellationToken ct = default)
  {
    var user = await reader.FindSubjectAsync(userId, ct);
    if (user == null) return false;
    if (user.IsRoot) return true;
    if (user.PersonalId == null || user.PersonalStatus is not (Status.Inserted or Status.Modified) ||
        !IpAddressRule.TryParseClient(clientIp, out var address)) return false;
    var rules = await reader.GetActiveRulesAsync(user.PersonalId.Value, ct);
    return rules.Any(value => IpAddressRule.TryParse(value, out var rule) && rule!.Contains(address!));
  }
}
