using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Guard.Core.Logging;

namespace Guard.Core.Services;

public sealed class SynchronizationService(IDbContextFactory<ApplicationDbContext> factory,
  IPermissionService permissions, ApiSynchronizationRunner runner, IAuditService audit, ApiCredentialProtector credentials,
  ILogger<SynchronizationService> logger) : ISynchronizationService
{
  public async Task CheckConnectionAsync(CancellationToken ct = default)
  {
    if (!(await permissions.GetCurrentAsync(ct)).IsRoot) throw new UnauthorizedAccessException("Проверка подключения доступна только Root.");
    await using var db = await factory.CreateDbContextAsync(ct);
    var settings = await db.SynchronizationSettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct);
    if (string.IsNullOrWhiteSpace(settings.ApiUsername) || settings.ProtectedApiPassword == null)
      throw new EntityRuleException("Сначала сохраните логин и пароль API.");
    await runner.CheckConnectionAsync(settings.ApiBaseUrl, settings.ApiUsername, credentials.Unprotect(settings.ProtectedApiPassword), ct);
  }

  public async Task<DirectoryActuality> GetActualityAsync(string readPermission, CancellationToken ct = default)
  {
    if (readPermission is not (Permissions.Classifiers.Read or Permissions.Subdivisions.Read or Permissions.Personals.Read))
      throw new ArgumentException("Неизвестная область актуальности.");
    await permissions.RequireAsync(readPermission, ct);
    await using var db = await factory.CreateDbContextAsync(ct);
    var mode = await db.SynchronizationSettings.AsNoTracking().Where(s => s.Id == 1).Select(s => s.Mode).SingleAsync(ct);
    var actual = await GlobalActualAtAsync(db, ct);
    await permissions.RequireAsync(readPermission, ct);
    return new(mode, actual);
  }

  private static Task<DateTime?> GlobalActualAtAsync(ApplicationDbContext db, CancellationToken ct) =>
    db.SynchronizationRuns.AsNoTracking().Where(r => r.PoolId == null && r.Status == SynchronizationStatus.Succeeded)
      .MaxAsync(r => r.CompletedAt, ct);

  public async Task<SynchronizationOverview> GetOverviewAsync(bool classifiers = false, bool includeChildren = true, CancellationToken ct = default, bool allSubdivisions = false)
  {
    var access = await permissions.GetCurrentAsync(ct);
    if (!access.IsRoot) throw new UnauthorizedAccessException("Управление синхронизацией доступно только Root.");
    await using var db = await factory.CreateDbContextAsync(ct);
    var settings = await db.SynchronizationSettings.AsNoTracking().Where(s => s.Id == 1)
      .Select(SynchronizationProjection.Settings).SingleAsync(ct);
    var last = await db.SynchronizationRuns.AsNoTracking().OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id)
      .Select(SynchronizationProjection.Run).FirstOrDefaultAsync(ct);
    var actual = await GlobalActualAtAsync(db, ct);
    if (!(await permissions.GetCurrentAsync(ct)).IsRoot) throw new UnauthorizedAccessException();
    return new(settings, null, actual, last, settings.Mode == DataManagementMode.Api);
  }

  public async Task SaveSettingsAsync(DataManagementMode mode, bool enabled, TimeOnly time, Guid version, CancellationToken ct = default, string? apiBaseUrl = null, int? apiPageSize = null, string? apiUsername = null, string? apiPassword = null, long? apiHeadSubdivisionId = null, bool? completeSnapshotGuaranteed = null)
  {
    var actor = await permissions.GetCurrentAsync(ct);
    if (!actor.IsRoot) throw new UnauthorizedAccessException("Настройки доступны только Root.");
    if (!Enum.IsDefined(mode)) throw new ArgumentException("Неизвестный режим управления.");
    if (apiHeadSubdivisionId is <= 0) throw new ArgumentException("Внешний ID головного подразделения должен быть положительным числом.");
    if (apiUsername != null && (apiUsername.Length > 256 || apiUsername.Contains(':') || apiUsername.Any(char.IsControl)))
      throw new ArgumentException("Логин API должен содержать до 256 символов без двоеточия и управляющих символов.");
    if (apiPassword != null && (apiPassword.Length > 4096 || apiPassword.Any(char.IsControl)))
      throw new ArgumentException("Пароль API должен содержать до 4096 символов без управляющих символов.");
    if (apiPageSize.HasValue && apiPageSize is < 1 or > 500)
      throw new ArgumentException("Размер страницы должен быть от 1 до 500 записей.");
    if (apiBaseUrl != null)
    {
      apiBaseUrl = apiBaseUrl.Trim();
      if (!(apiBaseUrl.Length == 0 && mode == DataManagementMode.Local) &&
          (apiBaseUrl.Length > 2048 || !Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var uri) ||
          uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(uri.Host) ||
          uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0))
        throw new ArgumentException("Укажите HTTPS-адрес API без пароля, параметров запроса и фрагмента.");
    }
    await using var db = await factory.CreateDbContextAsync(ct);
    await using var transaction = await db.Database.BeginTransactionAsync(ct);
    await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ApplicationDbContext.ManagementLock})", ct);
    var settings = await db.SynchronizationSettings.SingleAsync(s => s.Id == 1, ct);
    EntityInputValidation.CheckVersion(settings.Version, version);
    var sourceChanged = SynchronizationSettingsRules.SourceChanged(settings, apiBaseUrl, apiUsername);
    var previouslyConfirmed = settings.CompleteSnapshotGuaranteed;
    settings.CompleteSnapshotGuaranteed = !sourceChanged && (completeSnapshotGuaranteed ?? previouslyConfirmed);
    settings.Mode = mode; settings.ScheduleEnabled = enabled; settings.DailyTime = time;
    settings.ApiHeadSubdivisionId = apiHeadSubdivisionId;
    if (apiBaseUrl != null) settings.ApiBaseUrl = apiBaseUrl.TrimEnd('/');
    if (apiPageSize.HasValue) settings.ApiPageSize = apiPageSize.Value;
    if (apiUsername != null) settings.ApiUsername = apiUsername.Trim();
    if (!string.IsNullOrEmpty(apiPassword)) settings.ProtectedApiPassword = credentials.Protect(apiPassword);
    if (apiUsername != null && mode == DataManagementMode.Api &&
        (string.IsNullOrWhiteSpace(settings.ApiUsername) || string.IsNullOrEmpty(settings.ProtectedApiPassword)))
      throw new ArgumentException("Для управления из API укажите логин и пароль Basic auth.");
    settings.ModifiedBy = actor.UserId; settings.ModifiedAt = DateTime.UtcNow;
    await db.SaveChangesAsync(ct);
    await AuditTransaction.RecordAsync(db, audit, AuditEventData.Create(Guard.Core.Enums.AuditEventType.SynchronizationSettingsChanged, actor.UserId,
      details: $"Mode={mode}; ScheduleEnabled={enabled}; DailyTime={time:HH:mm}; ApiHeadSubdivisionId={apiHeadSubdivisionId}; CompleteSnapshotGuaranteed={settings.CompleteSnapshotGuaranteed}; PreviouslyConfirmed={previouslyConfirmed}; SourceChanged={sourceChanged}")
      with { ObjectType = "SynchronizationSettings", ObjectId = "1" }, ct);
    await AuditTransaction.CommitAsync(transaction, ct);
    logger.LogDebug("Настройки синхронизации {SettingsId} сохранены с аудитом", settings.Id);
  }

  public async Task<SynchronizationRunDto> RunAsync(CancellationToken ct = default, DateOnly? updatedSince = null, bool enqueue = false)
  {
    var actor = await permissions.GetCurrentAsync(ct);
    if (!actor.IsRoot) throw new UnauthorizedAccessException("Запуск синхронизации доступен только Root.");
    Guid? pool = null;
    if (!enqueue) return SynchronizationProjection.FromRun(await runner.RunAsync(actor.UserId, pool, false, ct, updatedSince));
    await using var queueDb = await factory.CreateDbContextAsync(ct);
    await using var transaction = await queueDb.Database.BeginTransactionAsync(ct);
    if (!await queueDb.Database.SqlQueryRaw<bool>($"SELECT pg_try_advisory_xact_lock({PostgresAdvisoryLocks.ApiSynchronization}) AS \"Value\"").SingleAsync(ct))
      throw new EntityRuleException("Синхронизация или диагностика уже выполняется.");
    await queueDb.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ApplicationDbContext.ManagementLock})", ct);
    var settings = await queueDb.SynchronizationSettings.SingleAsync(s => s.Id == 1, ct);
    if (settings.Mode != DataManagementMode.Api) throw new EntityRuleException("В локальном режиме синхронизация отключена.");
    SynchronizationSettingsRules.RequireSnapshotConfirmation(settings);
    if (await queueDb.SynchronizationRuns.AnyAsync(r => r.Status == SynchronizationStatus.Queued || r.Status == SynchronizationStatus.Running, ct))
      throw new EntityRuleException("Синхронизация уже ожидает запуска или выполняется.");
    var request = new SynchronizationRun { ActorId = actor.UserId, PoolId = pool, UpdatedSince = updatedSince,
      Status = SynchronizationStatus.Queued, QueuedAt = DateTime.UtcNow, StartedAt = DateTime.UtcNow,
      RequestedSettingsVersion = settings.Version, Phase = "Queued" };
    queueDb.Add(request); await queueDb.SaveChangesAsync(ct); await AuditTransaction.CommitAsync(transaction, ct);
    return SynchronizationProjection.FromRun(request);
  }

  public async Task<IReadOnlyList<SynchronizationRunDto>> GetHistoryAsync(CancellationToken ct = default)
  {
    var access = await permissions.GetCurrentAsync(ct);
    if (!access.IsRoot) throw new UnauthorizedAccessException("История синхронизации доступна только Root.");
    await using var db = await factory.CreateDbContextAsync(ct);
    var history = await db.SynchronizationRuns.AsNoTracking()
      .OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id).Take(20)
      .Select(SynchronizationProjection.Run).ToListAsync(ct);
    if (!(await permissions.GetCurrentAsync(ct)).IsRoot) throw new UnauthorizedAccessException();
    return history.AsReadOnly();
  }
}

public sealed class ApiSynchronizationRunner(IDbContextFactory<ApplicationDbContext> factory,
  IDirectoryApiClient client, IPermissionService permissions, IAuditService audit, ILogger<ApiSynchronizationRunner> logger, ApiCredentialProtector credentials, IHostApplicationLifetime? applicationLifetime = null, TimeProvider? timeProvider = null,
  IOperationAvailability? availability = null)
{
  public Task CheckConnectionAsync(string baseUrl, string username, string password, CancellationToken ct) => client.PingAsync(baseUrl, username, password, ct);

  public async Task RecoverInterruptedAsync(CancellationToken ct)
  {
    await using var db = await factory.CreateDbContextAsync(ct);
    await db.Database.OpenConnectionAsync(ct);
    if (!await db.Database.SqlQueryRaw<bool>($"SELECT pg_try_advisory_lock({PostgresAdvisoryLocks.ApiSynchronization}) AS \"Value\"").SingleAsync(ct)) return;
    try
    {
      await db.SynchronizationRuns.Where(r => r.Status == SynchronizationStatus.Running)
        .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, SynchronizationStatus.Interrupted)
          .SetProperty(r => r.CompletedAt, DateTime.UtcNow).SetProperty(r => r.Error, "Предыдущий процесс завершился до окончания синхронизации."), ct);
    }
    finally { await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({PostgresAdvisoryLocks.ApiSynchronization})", CancellationToken.None); }
  }

  public async Task<SynchronizationRun> RunAsync(string actor, Guid? pool, bool scheduled, CancellationToken ct, DateOnly? updatedSince = null, Guid? queuedRunId = null)
  {
    if (availability != null) await availability.RequireWorkingAsync(ct);
    var clock = timeProvider ?? TimeProvider.System;
    await using var db = await factory.CreateDbContextAsync(ct);
    db.IsSynchronization = true;
    await db.Database.OpenConnectionAsync(ct);
    if (!await db.Database.SqlQueryRaw<bool>($"SELECT pg_try_advisory_lock({PostgresAdvisoryLocks.ApiSynchronization}) AS \"Value\"").SingleAsync(ct))
      throw new EntityRuleException("Синхронизация уже выполняется. Дождитесь её завершения.");
    SynchronizationRun? run = null;
    var committed = false;
    CancellationTokenSource? fetchCancellation = null;
    IDisposable? runScope = null;
    var elapsed = System.Diagnostics.Stopwatch.StartNew();
    var phase = "Initialization";
    var settingsChanged = 0;
    using var callerCancellation = ct.Register(() => logger.LogWarning("Синхронизация {RunId}: отменён токен инициатора, этап {SyncPhase}, причина {CancellationReason}, длительность {SyncElapsedMs} мс", run?.Id, phase,
      applicationLifetime?.ApplicationStopping.IsCancellationRequested == true ? "ApplicationStopping" : scheduled ? "WorkerStopping" : "ManualCallerToken", elapsed.Elapsed.TotalMilliseconds));
    try
    {
      if (queuedRunId.HasValue)
      {
        run = await db.SynchronizationRuns.SingleAsync(r => r.Id == queuedRunId.Value, ct);
        if (run.Status != SynchronizationStatus.Queued) return run;
        actor = run.ActorId; pool = run.PoolId; updatedSince = run.UpdatedSince; scheduled = false;
      }
      var settings = await db.SynchronizationSettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct);
      if (settings.Mode != DataManagementMode.Api) throw new EntityRuleException("В локальном режиме синхронизация отключена.");
      SynchronizationSettingsRules.RequireSnapshotConfirmation(settings);
      if (run?.RequestedSettingsVersion is Guid expectedVersion && expectedVersion != settings.Version)
        throw new EntityRuleException("Настройки изменились после постановки в очередь. Запустите синхронизацию повторно.");
      if (!scheduled)
      {
        var access = await permissions.GetForUserAsync(actor, ct);
        if (!access.IsRoot || pool.HasValue)
          throw new UnauthorizedAccessException("Разрешение запуска отозвано.");
        if (pool.HasValue)
        {
          var actualPool = await db.Users.Where(u => u.Id == actor).Select(u => u.Personal == null ? null : u.Personal.SubdivisionId).SingleOrDefaultAsync(ct);
          if (actualPool != pool) throw new UnauthorizedAccessException("Область пользователя изменилась до запуска.");
        }
      }
      if (scheduled && (!settings.ScheduleEnabled || !SynchronizationSchedule.IsDue(settings, clock.GetUtcNow())))
        throw new EntityRuleException("Плановый запуск больше не требуется.");
      var startedAt = clock.GetUtcNow().UtcDateTime;
      if (scheduled) updatedSince = SynchronizationSchedule.UpdatedSince(startedAt);
      if (updatedSince.HasValue && pool.HasValue) throw new EntityRuleException("Синхронизация с датой доступна только для всей организации.");
      // Только владелец глобальной блокировки может объявить прежний запуск прерванным.
      await db.SynchronizationRuns.Where(r => r.Status == SynchronizationStatus.Running)
        .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, SynchronizationStatus.Interrupted)
          .SetProperty(r => r.CompletedAt, DateTime.UtcNow).SetProperty(r => r.Error, "Предыдущий процесс завершился до окончания синхронизации."), ct);
      if (run == null)
      {
        run = new() { ActorId = actor, PoolId = pool, Scheduled = scheduled, StartedAt = startedAt, UpdatedSince = updatedSince };
        db.SynchronizationRuns.Add(run);
      }
      else { run.Status = SynchronizationStatus.Running; run.StartedAt = startedAt; }
      run.Phase = "Fetch";
      runScope = logger.BeginScope(new Dictionary<string, object> { ["RunId"] = run.Id, ["SyncScheduled"] = scheduled });
      logger.LogInformation("Синхронизация {RunId}: фильтр UpdatedAt {UpdatedSince}, полная выгрузка {FullSnapshot}", run.Id, updatedSince, !updatedSince.HasValue);
      logger.LogInformation("Синхронизация {RunId}: начало, по расписанию {SyncScheduled}, пул {PoolId}, версия настроек {SettingsVersion}, размер страницы {ApiPageSize}", run.Id, scheduled, pool, settings.Version, settings.ApiPageSize);
      await db.SaveChangesAsync(ct);
      using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
      fetchCancellation = cancellation;
      using var monitorStop = CancellationTokenSource.CreateLinkedTokenSource(ct);
      var monitor = WatchSettingsAsync(settings.Version, cancellation, monitorStop.Token, () => Interlocked.Exchange(ref settingsChanged, 1));
      phase = "Fetch";
      ApiSnapshot snapshot;
      try { snapshot = await client.FetchAsync(settings.ApiBaseUrl, settings.ApiPageSize, cancellation.Token,
        settings.ApiUsername, settings.ProtectedApiPassword == null ? "" : credentials.Unprotect(settings.ProtectedApiPassword), updatedSince, settings.ApiHeadSubdivisionId,
        async (stage, token) => { run.Phase = stage; await db.SaveChangesAsync(token); });
        snapshot = snapshot with { UpdatedSince = updatedSince, HeadSubdivisionId = settings.ApiHeadSubdivisionId };
        run.Phase = "Dependencies"; await db.SaveChangesAsync(cancellation.Token);
        var classifierKeys = await db.Classifiers.AsNoTracking().Select(c => new { c.Type, c.Code }).ToListAsync(cancellation.Token);
        var subdivisionKeys = await db.Subdivisions.AsNoTracking().Where(s => s.ExternalId.HasValue).Select(s => s.ExternalId!.Value).ToListAsync(cancellation.Token);
        snapshot = await client.ResolveDependenciesAsync(snapshot, classifierKeys.Select(c => (c.Type, c.Code)).ToHashSet(), subdivisionKeys.ToHashSet(),
          settings.ApiBaseUrl, settings.ApiUsername, settings.ProtectedApiPassword == null ? "" : credentials.Unprotect(settings.ProtectedApiPassword), cancellation.Token);
      }
      finally
      {
        await monitorStop.CancelAsync();
        try { await monitor; } catch (OperationCanceledException) when (monitorStop.IsCancellationRequested) { }
      }
      cancellation.Token.ThrowIfCancellationRequested();
      logger.LogInformation("Синхронизация {RunId}: выгрузка получена, классификаторов {ClassifierCount}, подразделений {SubdivisionCount}, сотрудников {PersonnelCount}, длительность {SyncElapsedMs} мс", run.Id, snapshot.Classifiers.Count, snapshot.Subdivisions.Count, snapshot.Personnel.Count, elapsed.Elapsed.TotalMilliseconds);
      phase = "Apply";
      run.Phase = phase; await db.SaveChangesAsync(ct);
      await using var transaction = await db.Database.BeginTransactionAsync(ct);
      try
      {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ApplicationDbContext.ManagementLock})", ct);
        var current = await db.SynchronizationSettings.SingleAsync(s => s.Id == 1, ct);
        if (current.Mode != DataManagementMode.Api || current.Version != settings.Version)
          throw new EntityRuleException("Настройки изменились. Запустите синхронизацию повторно.");
        SynchronizationSettingsRules.RequireSnapshotConfirmation(current);
        if (!scheduled)
        {
          var access = await permissions.GetForUserAsync(actor, ct);
          if (!access.IsRoot || pool.HasValue)
            throw new UnauthorizedAccessException("Разрешение запуска отозвано.");
        }
        if (pool.HasValue)
        {
          // Перепроверяем разрешение и привязку перед записью, используя свежий scoped-сервис.
          var personalPool = await db.Users.Where(u => u.Id == actor).Select(u => u.Personal == null ? null : u.Personal.SubdivisionId).SingleOrDefaultAsync(ct);
          if (personalPool != pool) throw new UnauthorizedAccessException("Область пользователя изменилась.");
        }
        var coveredIds = await ApiSnapshotApplier.ApplyAsync(db, snapshot, run, ct);
        run.Status = SynchronizationStatus.Succeeded; run.CompletedAt = clock.GetUtcNow().UtcDateTime;
        current.ClassifiersSynchronizedAt = run.CompletedAt;
        if (!pool.HasValue)
        {
          if (updatedSince.HasValue)
          {
            var previous = new[] { current.LastFullSynchronizedAt, current.LastChangesSynchronizedAt }.Where(d => d.HasValue).Max();
            if (previous.HasValue)
            {
              var coveredDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(previous.Value, TimeZoneInfo.Local));
              if (updatedSince.Value > coveredDate && (!current.RecoveryRequiredFrom.HasValue || coveredDate < current.RecoveryRequiredFrom))
                current.RecoveryRequiredFrom = coveredDate;
            }
            if (current.RecoveryRequiredFrom.HasValue && updatedSince.Value <= current.RecoveryRequiredFrom.Value)
              current.RecoveryRequiredFrom = null;
            current.LastChangesSynchronizedAt = run.CompletedAt;
          }
          else { current.LastFullSynchronizedAt = run.CompletedAt; current.RecoveryRequiredFrom = null; }
        }
        run.Phase = "Completed";
        if (scheduled) current.LastScheduledDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(run.StartedAt, TimeZoneInfo.Local));
        await db.SaveChangesAsync(ct);
        await db.Subdivisions.Where(s => coveredIds.Contains(s.Id))
          .ExecuteUpdateAsync(s => s.SetProperty(p => p.SynchronizedAt, run.CompletedAt), ct);
        if (availability != null) await availability.RequireWorkingAsync(ct);
        await AuditTransaction.RecordAsync(db, audit, AuditEventData.Create(Guard.Core.Enums.AuditEventType.SynchronizationCompleted,
          scheduled ? null : actor, details: $"RunId={run.Id}; PoolId={pool}; Status={run.Status}; Archived={run.Archived}; Restored={run.Restored}")
          with { Actor = scheduled ? null : actor, ActorKind = scheduled ? "system" : "user", ObjectType = "SynchronizationRun", ObjectId = run.Id.ToString() }, ct);
        await AuditTransaction.CommitAsync(transaction, ct); committed = true;
        phase = "Committed";
      }
      catch (OperationCommitUnknownException) { committed = true; throw; }
      catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
      logger.LogInformation("Синхронизация {RunId} завершена: добавлено {Added}, изменено {Updated}, архивировано {Archived}", run.Id, run.Added, run.Updated, run.Archived);
      logger.LogInformation("Синхронизация {RunId}: событий выхода {ExitEventCount}, повторных {RepeatedExitEvents}, неизвестных сотрудников {UnknownExitEmployees}, длительность {SyncElapsedMs} мс",
        run.Id, run.ExitEvents, run.RepeatedExitEvents, run.UnknownExitEmployees, elapsed.Elapsed.TotalMilliseconds);
      return run;
    }
    catch (Exception ex) when (run != null && !committed)
    {
      db.ChangeTracker.Clear();
      run = await db.SynchronizationRuns.SingleAsync(r => r.Id == run.Id, CancellationToken.None);
      run.Status = ex is OperationCanceledException && (ct.IsCancellationRequested || fetchCancellation?.IsCancellationRequested == true)
        ? SynchronizationStatus.Cancelled : SynchronizationStatus.Failed;
      run.CompletedAt = DateTime.UtcNow; run.Added = run.Updated = run.Archived = run.Restored = run.Deactivated = 0;
      run.ExitEvents = run.RepeatedExitEvents = run.UnknownExitEmployees = 0; run.Phase = phase;
      run.Error = ex is EntityRuleException or ArgumentException ? ex.Message :
        run.Status == SynchronizationStatus.Cancelled ? "Синхронизация отменена." : ex is OperationCanceledException ?
        "Превышено время ожидания ответа API." : "Не удалось выполнить синхронизацию. Подробности доступны в техническом журнале.";
      await db.SaveChangesAsync(CancellationToken.None);
      logger.LogWarning("Синхронизация {RunId}: завершение с ошибкой, этап {SyncPhase}, статус {SyncStatus}, причина {CancellationReason}, токен инициатора {CallerTokenCancelled}, токен загрузки {FetchTokenCancelled}, остановка приложения {ApplicationStopping}, длительность {SyncElapsedMs} мс", run.Id, phase, run.Status,
        Volatile.Read(ref settingsChanged) != 0 ? "SettingsChanged" : ct.IsCancellationRequested ? "CallerToken" : ex is OperationCanceledException ? "HttpTimeoutOrTransportCancellation" : "Failure",
        ct.IsCancellationRequested, fetchCancellation?.IsCancellationRequested, applicationLifetime?.ApplicationStopping.IsCancellationRequested, elapsed.Elapsed.TotalMilliseconds);
      logger.LogError(ex, "Ошибка синхронизации {RunId}", run.Id);
      return run;
    }
    finally
    {
      await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({PostgresAdvisoryLocks.ApiSynchronization})", CancellationToken.None);
      runScope?.Dispose();
    }
  }

  private async Task WatchSettingsAsync(Guid version, CancellationTokenSource operation, CancellationToken ct, Action settingsChanged)
  {
    while (!ct.IsCancellationRequested)
    {
      await Task.Delay(TimeSpan.FromSeconds(2), ct);
      await using var db = await factory.CreateDbContextAsync(ct);
      var current = await db.SynchronizationSettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct);
      if (current.Version != version)
      {
        settingsChanged();
        logger.LogWarning("Синхронизация: настройки изменены, прежняя версия {OldSettingsVersion}, новая версия {NewSettingsVersion}, режим {ManagementMode}; отмена загрузки", version, current.Version, current.Mode);
        await operation.CancelAsync(); return;
      }
    }
  }
}
