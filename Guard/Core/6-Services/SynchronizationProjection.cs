using System.Linq.Expressions;
using Guard.Core.Entities;
using Guard.Core.Services.DTOs;

namespace Guard.Core.Services;

internal static class SynchronizationProjection
{
  internal static readonly Expression<Func<SynchronizationSettings, SynchronizationSettingsDto>> Settings = s => new()
  {
    Version = s.Version, Mode = s.Mode, ScheduleEnabled = s.ScheduleEnabled,
    ApiBaseUrl = s.ApiBaseUrl, ApiPageSize = s.ApiPageSize, ApiHeadSubdivisionId = s.ApiHeadSubdivisionId,
    ApiUsername = s.ApiUsername, CompleteSnapshotGuaranteed = s.CompleteSnapshotGuaranteed,
    HasApiPassword = s.ProtectedApiPassword != null && s.ProtectedApiPassword != "",
    DailyTime = s.DailyTime, LastScheduledDate = s.LastScheduledDate,
    ClassifiersSynchronizedAt = s.ClassifiersSynchronizedAt, LastFullSynchronizedAt = s.LastFullSynchronizedAt,
    LastChangesSynchronizedAt = s.LastChangesSynchronizedAt, RecoveryRequiredFrom = s.RecoveryRequiredFrom
  };

  internal static readonly Expression<Func<SynchronizationRun, SynchronizationRunDto>> Run = r => new()
  {
    Id = r.Id, PoolId = r.PoolId, Scheduled = r.Scheduled, UpdatedSince = r.UpdatedSince,
    StartedAt = r.StartedAt, QueuedAt = r.QueuedAt, Phase = r.Phase, CompletedAt = r.CompletedAt,
    Status = r.Status, Added = r.Added, Updated = r.Updated, Archived = r.Archived,
    Restored = r.Restored, Deactivated = r.Deactivated, ExitEvents = r.ExitEvents,
    RepeatedExitEvents = r.RepeatedExitEvents, UnknownExitEmployees = r.UnknownExitEmployees, Error = r.Error
  };

  internal static readonly Func<SynchronizationRun, SynchronizationRunDto> FromRun = Run.Compile();
}
