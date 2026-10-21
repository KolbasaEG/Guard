using Guard.Core.Entities;

internal static class NotificationRuleTests
{
  public static void Run(Action<bool, string> check)
  {
    var incident = new NotificationIncident { RequiredSince = new(2026, 10, 18) };
    check(!incident.Covers(new(2026, 10, 19)) && incident.Covers(new(2026, 10, 18)) && incident.Covers(new(2026, 10, 17)),
      "incremental synchronization must cover failed period to resolve incident");
    incident.FullSnapshotRequired = true;
    check(!incident.Covers(new(2020, 1, 1)) && incident.Covers(null), "full snapshot incident requires full recovery");
  }
}
