using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;

internal static class IpAccessBoundaryTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var personalId = Guid.NewGuid();
    using var cancellation = new CancellationTokenSource();
    var reader = new Reader { Token = cancellation.Token, PersonalId = personalId };
    var service = new IpAccessService(reader);
    foreach (var (subject, client) in new (IpAccessSubject?, string?)[] {
      (null, "192.168.1.1"), (new(false, null, Status.Inserted), "192.168.1.1"),
      (new(false, personalId, null), "192.168.1.1"),
      (new(false, personalId, Status.Inserted), "invalid"),
      (new(false, personalId, Status.Inserted), "192.168.1.0/24") }) {
      reader.Subject = subject;
      check(!await service.IsAllowedAsync("subject", client, reader.Token), "invalid IP access subject or client is denied");
      check(reader.RuleReads == 0, "denied subject/client never loads IP assignments");
    }
    foreach (var status in Enum.GetValues<Status>().Append((Status)999)) {
      reader.Subject = new(false, personalId, status);
      reader.Rules = ["malformed", "192.168.1.0/24"];
      var before = reader.RuleReads;
      var active = status is Status.Inserted or Status.Modified;
      check(await service.IsAllowedAsync("subject", "::ffff:192.168.1.1", reader.Token) == active,
        $"independent rule explicitly handles employee status {status}");
      check(reader.RuleReads == before + (active ? 1 : 0), "inactive employee does not load rules");
    }
    reader.Subject = new(true, null, Status.Blocked);
    var beforeRoot = reader.RuleReads;
    check(await service.IsAllowedAsync("subject", null, reader.Token), "Root bypasses missing employee and client IP");
    check(reader.RuleReads == beforeRoot, "Root does not load IP assignments");
    reader.Subject = new(false, personalId, Status.Modified);
    foreach (var (rules, client, allowed) in new (string[], string, bool)[] {
      ([], "192.168.1.1", false), (["invalid"], "192.168.1.1", false),
      (["192.168.1.0/24"], "192.168.2.1", false),
      (["2001:db8:1::/64"], "2001:db8:1::42", true),
      (["2001:db8:1::/64"], "2001:db8:2::42", false) }) {
      reader.Rules = rules;
      check(await service.IsAllowedAsync("subject", client, reader.Token) == allowed,
        "independent gate handles empty, invalid and IPv6 rules");
    }
    reader.Failure = new InvalidOperationException("synthetic reader failure");
    try { await service.IsAllowedAsync("subject", "192.168.1.1", reader.Token); check(false, "reader failure propagates"); }
    catch (InvalidOperationException error) { check(ReferenceEquals(error, reader.Failure), "reader failure is not converted to access success"); }
    reader.Failure = null;
    reader.CancelRules = true;
    try { await service.IsAllowedAsync("subject", "192.168.1.1", reader.Token); check(false, "rule read cancellation propagates"); }
    catch (OperationCanceledException error) { check(error.CancellationToken == reader.Token, "rule read cancellation retains token"); }
    reader.CancelRules = false;
    cancellation.Cancel();
    try { await service.IsAllowedAsync("subject", "192.168.1.1", reader.Token); check(false, "subject read cancellation propagates"); }
    catch (OperationCanceledException error) { check(error.CancellationToken == reader.Token, "subject read cancellation retains token"); }
    check(reader.ArgumentsValid, "both reader calls receive unchanged ID and cancellation token");
  }

  private sealed class Reader : IIpAccessReader
  {
    public CancellationToken Token { get; init; }
    public Guid PersonalId { get; init; }
    public IpAccessSubject? Subject { get; set; }
    public IReadOnlyList<string> Rules { get; set; } = [];
    public int RuleReads { get; private set; }
    public bool ArgumentsValid { get; private set; } = true;
    public Exception? Failure { get; set; }
    public bool CancelRules { get; set; }
    public Task<IpAccessSubject?> FindSubjectAsync(string userId, CancellationToken ct = default)
    {
      ArgumentsValid &= userId == "subject" && ct == Token;
      ct.ThrowIfCancellationRequested();
      if (Failure != null) throw Failure;
      return Task.FromResult(Subject);
    }
    public Task<IReadOnlyList<string>> GetActiveRulesAsync(Guid personalId, CancellationToken ct = default)
    {
      ArgumentsValid &= personalId == PersonalId && ct == Token;
      RuleReads++;
      if (CancelRules) throw new OperationCanceledException(ct);
      return Task.FromResult(Rules);
    }
  }
}
