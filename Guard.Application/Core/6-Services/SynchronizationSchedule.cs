using Guard.Core.Entities;

namespace Guard.Core.Services;

public static class SynchronizationSchedule
{
  public static DateOnly UpdatedSince(DateTime startedAtUtc) =>
    DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(startedAtUtc, TimeZoneInfo.Local)).AddDays(-1);
  public static bool IsDue(SynchronizationSettings settings, DateTimeOffset utcNow)
  {
    var local = TimeZoneInfo.ConvertTime(utcNow, TimeZoneInfo.Local);
    return settings.Mode == DataManagementMode.Api && settings.CompleteSnapshotGuaranteed && settings.ScheduleEnabled &&
      TimeOnly.FromDateTime(local.DateTime) >= settings.DailyTime &&
      (!settings.LastScheduledDate.HasValue || settings.LastScheduledDate.Value < DateOnly.FromDateTime(local.DateTime));
  }
}
