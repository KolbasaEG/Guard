using Guard.Core.Entities;
using Guard.Core.Services;

internal static class SynchronizationScheduleTests
{
  public static void Run(Action<bool, string> check)
  {
    var settings = new SynchronizationSettings { Mode = DataManagementMode.Api, CompleteSnapshotGuaranteed = true, ScheduleEnabled = true, DailyTime = new(2, 0) };
    var localDay = new DateTime(2026, 10, 9);
    DateTimeOffset Utc(DateTime local) => new(TimeZoneInfo.ConvertTimeToUtc(local, TimeZoneInfo.Local));
    check(!SynchronizationSchedule.IsDue(settings, Utc(localDay.AddHours(1))), "schedule waits for server local time");
    check(SynchronizationSchedule.IsDue(settings, Utc(localDay.AddHours(2))), "schedule runs at server 02:00");
    settings.LastScheduledDate = DateOnly.FromDateTime(localDay);
    check(!SynchronizationSchedule.IsDue(settings, Utc(localDay.AddHours(3))), "successful scheduled date prevents duplicate execution");
    settings.Mode = DataManagementMode.Local;
    check(!SynchronizationSchedule.IsDue(settings, Utc(localDay.AddDays(1).AddHours(3))), "local mode disables schedule");

    CheckBoundaries(check);
  }
  private static void CheckBoundaries(Action<bool, string> check)
  {
    var day = new DateOnly(2026, 10, 9);
    DateTimeOffset At(DateOnly date, TimeOnly time) =>
      new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time), TimeZoneInfo.Local));

    foreach (var mode in new[] { DataManagementMode.Api, DataManagementMode.Local, (DataManagementMode)999 })
    foreach (var guaranteed in new[] { false, true })
    foreach (var enabled in new[] { false, true })
    foreach (var last in new DateOnly?[] { null, day.AddDays(-1), day, day.AddDays(1) })
    foreach (var tick in new[] { -1L, 0L, 1L })
    {
      var scheduled = new TimeOnly(12, 0);
      var settings = new SynchronizationSettings
      {
        Mode = mode, CompleteSnapshotGuaranteed = guaranteed, ScheduleEnabled = enabled,
        DailyTime = scheduled, LastScheduledDate = last
      };
      var now = At(day, scheduled.Add(TimeSpan.FromTicks(tick)));
      var expected = mode == DataManagementMode.Api && guaranteed && enabled &&
        (last == null || last < day) && tick >= 0;
      check(SynchronizationSchedule.IsDue(settings, now) == expected,
        "schedule requires API, snapshot guarantee, enabled state, elapsed time and an earlier scheduled date");
      check(SynchronizationSchedule.IsDue(settings, now.ToOffset(TimeSpan.FromHours(-7))) == expected &&
        SynchronizationSchedule.IsDue(settings, now.ToOffset(TimeSpan.FromHours(11))) == expected,
        "schedule uses the server date and time independently of input offset");
      check(settings.Mode == mode && settings.CompleteSnapshotGuaranteed == guaranteed &&
        settings.ScheduleEnabled == enabled && settings.DailyTime == scheduled && settings.LastScheduledDate == last,
        "schedule evaluation does not mark a run or change its settings");
    }

    foreach (var date in new[] { new DateOnly(2026, 1, 1), new DateOnly(2028, 2, 29),
      new DateOnly(2028, 3, 1), new DateOnly(2026, 12, 31) })
    {
      var settings = new SynchronizationSettings
      {
        Mode = DataManagementMode.Api, CompleteSnapshotGuaranteed = true, ScheduleEnabled = true,
        DailyTime = TimeOnly.MinValue, LastScheduledDate = date.AddDays(-1)
      };
      var midnight = At(date, TimeOnly.MinValue);
      check(SynchronizationSchedule.IsDue(settings, midnight), "new local date permits a midnight schedule");
      settings.LastScheduledDate = date;
      check(!SynchronizationSchedule.IsDue(settings, midnight) &&
        !SynchronizationSchedule.IsDue(settings, At(date, TimeOnly.MaxValue)),
        "stored local date blocks the whole calendar day");
      check(SynchronizationSchedule.IsDue(settings, At(date.AddDays(1), TimeOnly.MinValue)),
        "next local date permits the next daily run across month, year and leap day");
      foreach (var time in new[] { TimeOnly.MinValue, new TimeOnly(12, 0), TimeOnly.MaxValue })
      {
        var utc = At(date, time).UtcDateTime;
        check(SynchronizationSchedule.UpdatedSince(utc) == date.AddDays(-1),
          "incremental date is the previous local calendar day throughout the day");
        check(SynchronizationSchedule.UpdatedSince(DateTime.SpecifyKind(utc, DateTimeKind.Unspecified)) == date.AddDays(-1),
          "legacy unspecified UTC input retains the same incremental date");
      }
    }

    var late = new SynchronizationSettings
    {
      Mode = DataManagementMode.Api, CompleteSnapshotGuaranteed = true, ScheduleEnabled = true,
      DailyTime = TimeOnly.MaxValue, LastScheduledDate = null
    };
    check(!SynchronizationSchedule.IsDue(late, At(day, TimeOnly.MaxValue.Add(TimeSpan.FromTicks(-1)))) &&
      SynchronizationSchedule.IsDue(late, At(day, TimeOnly.MaxValue)),
      "last tick of the local day is an inclusive schedule boundary");
    late.DailyTime = new TimeOnly(2, 0);
    check(SynchronizationSchedule.IsDue(late, At(day, new TimeOnly(23, 0))),
      "missed daily time remains due later that day");
  }
}
