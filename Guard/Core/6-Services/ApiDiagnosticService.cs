using System.Diagnostics;
using System.Globalization;
using Guard.Core.Contexts;
using Guard.Core.Identity;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public sealed class ApiDiagnosticService(IDbContextFactory<ApplicationDbContext> factory,
  IPermissionService permissions, IDirectoryApiClient client, ApiCredentialProtector credentials,
  ILogger<ApiDiagnosticService> logger) : IApiDiagnosticService
{
  public async Task<ApiDiagnosticResult> RunAsync(DateOnly updatedSince, CancellationToken ct = default, IProgress<string>? progress = null)
  {
    if (!(await permissions.GetCurrentAsync(ct)).IsRoot)
      throw new UnauthorizedAccessException("Диагностика доступна только Root.");
    if (updatedSince == default) throw new ArgumentException("Укажите дату UpdatedAt для диагностики.");
    if (!logger.IsEnabled(LogLevel.Information))
      throw new EntityRuleException("Для полного протокола диагностики включите уровень логирования Information.");
    await using var db = await factory.CreateDbContextAsync(ct);
    await db.Database.OpenConnectionAsync(ct);
    if (!await db.Database.SqlQueryRaw<bool>($"SELECT pg_try_advisory_lock({PostgresAdvisoryLocks.ApiSynchronization}) AS \"Value\"").SingleAsync(ct))
      throw new EntityRuleException("Уже выполняется синхронизация или диагностика. Дождитесь завершения.");
    var id = Guid.CreateVersion7();
    using var scope = logger.BeginScope(new Dictionary<string, object> { ["DiagnosticRunId"] = id,
      ["DiagnosticUpdatedAt"] = updatedSince.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
    var elapsed = Stopwatch.StartNew();
    var phase = "Initialization";
    ApiSnapshot? snapshot = null;
    var analysisComplete = false;
    var issueCount = 0;
    try
    {
      var settings = await db.SynchronizationSettings.AsNoTracking().SingleAsync(s => s.Id == 1, ct);
      logger.LogInformation("Диагностика {DiagnosticRunId}: начало, UpdatedAt {DiagnosticUpdatedAt}, размер страницы {ApiPageSize}, версия настроек {SettingsVersion}; данные не применяются",
        id, updatedSince.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), settings.ApiPageSize, settings.Version);
      phase = "Fetch";
      snapshot = await client.FetchDiagnosticAsync(settings.ApiBaseUrl, settings.ApiPageSize, settings.ApiUsername,
        settings.ProtectedApiPassword == null ? "" : credentials.Unprotect(settings.ProtectedApiPassword), updatedSince, ct, progress);
      snapshot = snapshot with { HeadSubdivisionId = settings.ApiHeadSubdivisionId };
      logger.LogInformation("Диагностика {DiagnosticRunId}: головное подразделение API {ApiHeadSubdivisionId}", id, settings.ApiHeadSubdivisionId);
      logger.LogInformation("Диагностика {DiagnosticRunId}: событий выхода {ExitEventCount}", id, snapshot.ExitEvents.Count);
      logger.LogInformation("Диагностика {DiagnosticRunId}: все страницы получены, FetchComplete {FetchComplete}, классификаторов {ClassifierCount}, подразделений {SubdivisionCount}, сотрудников {PersonnelCount}, длительность {DiagnosticElapsedMs} мс",
        id, true, snapshot.Classifiers.Count, snapshot.Subdivisions.Count, snapshot.Personnel.Count, elapsed.Elapsed.TotalMilliseconds);
      phase = "Analyze";
      progress?.Report("Все страницы получены. Анализ данных и ссылок");
      if (!(await permissions.GetCurrentAsync(ct)).IsRoot)
        throw new UnauthorizedAccessException("Доступ к диагностике отозван.");
      var parents = await db.Subdivisions.AsNoTracking().Where(s => s.ExternalId.HasValue)
        .Select(s => new { Id = s.ExternalId!.Value, ParentId = s.ExternalParentId }).ToListAsync(ct);
      var classifiers = await db.Classifiers.AsNoTracking().Select(c => new { c.Type, c.Code }).ToListAsync(ct);
      var issues = ApiDiagnosticAnalyzer.Analyze(snapshot, parents.ToDictionary(s => s.Id, s => s.ParentId),
        classifiers.Select(c => (c.Type, c.Code)).ToHashSet(), ct);
      foreach (var root in snapshot.Subdivisions.Where(s => s.ParentId is null or 0 || s.Id == snapshot.HeadSubdivisionId))
      {
        ct.ThrowIfCancellationRequested();
        logger.LogInformation("Диагностика {DiagnosticRunId}: корень подразделения {ExternalId}, ParentId {ExternalParentId}", id, root.Id, root.ParentId);
      }
      foreach (var issue in issues)
      {
        ct.ThrowIfCancellationRequested();
        if (!logger.IsEnabled(LogLevel.Information))
          throw new EntityRuleException("Уровень логирования изменён во время диагностики. Протокол неполный.");
        logger.LogInformation("Диагностика {DiagnosticRunId}: нарушение {DiagnosticIssueCode}, ресурс {ApiResource}, запись {ExternalRecordId}, поле {DiagnosticField}, ссылка {ExternalReferenceId}, найдена локально {FoundLocally}",
          id, issue.Code, issue.Resource, issue.RecordId, issue.Field, issue.ReferenceId, issue.FoundLocally);
      }
      foreach (var group in issues.GroupBy(i => new { i.Resource, i.Code, i.FoundLocally }))
        logger.LogInformation("Диагностика {DiagnosticRunId}: сводка {ApiResource}, {DiagnosticIssueCode}, найдена локально {FoundLocally}, количество {DiagnosticIssueCount}",
          id, group.Key.Resource, group.Key.Code, group.Key.FoundLocally, group.Count());
      ct.ThrowIfCancellationRequested();
      if (!logger.IsEnabled(LogLevel.Information))
        throw new EntityRuleException("Уровень логирования изменён во время диагностики. Протокол неполный.");
      issueCount = issues.Count;
      analysisComplete = true;
      return new(id, true, true, false, snapshot.Classifiers.Count, snapshot.Subdivisions.Count, snapshot.Personnel.Count, issueCount, ExitEvents: snapshot.ExitEvents.Count);
    }
    catch (Exception ex)
    {
      var cancelled = ex is OperationCanceledException && ct.IsCancellationRequested;
      // Do not log raw JSON/exception messages: transport/deserialization errors can contain source data.
      logger.LogWarning("Диагностика {DiagnosticRunId}: не завершена, этап {DiagnosticPhase}, отменена {DiagnosticCancelled}, тип ошибки {DiagnosticExceptionType}, HTTP {ApiStatusCode}",
        id, phase, cancelled, ex.GetType().Name, (ex as HttpRequestException)?.StatusCode);
      return new(id, snapshot != null, false, cancelled, snapshot?.Classifiers.Count ?? 0,
        snapshot?.Subdivisions.Count ?? 0, snapshot?.Personnel.Count ?? 0, 0,
        cancelled ? "Диагностика отменена. Анализ не завершён." : "Диагностика не завершена. Проверьте протокол по идентификатору запуска.");
    }
    finally
    {
      logger.LogInformation("Диагностика {DiagnosticRunId}: конец протокола, FetchComplete {FetchComplete}, AnalysisComplete {AnalysisComplete}, нарушений {DiagnosticIssueCount}, длительность {DiagnosticElapsedMs} мс",
        id, snapshot != null, analysisComplete, issueCount, elapsed.Elapsed.TotalMilliseconds);
      await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({PostgresAdvisoryLocks.ApiSynchronization})", CancellationToken.None);
    }
  }
}
