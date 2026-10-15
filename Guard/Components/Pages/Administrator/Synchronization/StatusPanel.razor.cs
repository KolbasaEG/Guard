using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.Administrator.Synchronization;

public partial class StatusPanel : IAsyncDisposable
{
  [Inject] protected ISynchronizationService Service { get; set; } = default!;
  [Inject] protected NotificationService Notifications { get; set; } = default!;
  [Inject] protected ILogger<StatusPanel> Logger { get; set; } = default!;
  [Parameter] public bool Classifiers { get; set; }
  [Parameter] public bool IncludeChildren { get; set; } = true;
  [Parameter] public bool AllSubdivisions { get; set; }
  [Parameter] public EventCallback<bool> ApiModeChanged { get; set; }
  [Parameter] public EventCallback Synchronized { get; set; }
  [Parameter] public EventCallback LastAttemptChanged { get; set; }
  [Parameter] public EventCallback ManualRefreshRequested { get; set; }
  [Parameter] public EventCallback RefreshCompleted { get; set; }
  // True means LastAttemptChanged already requested a reload in this cycle.
  [Parameter] public EventCallback<bool> PollingCycleCompleted { get; set; }
  [CascadingParameter] public UserAccessSnapshot? Access { get; set; }
  private SynchronizationOverview? overview;
  private (Guid? Id, SynchronizationStatus? Status, string? Phase, DateTime? CompletedAt)? lastSuccessfulAttempt;
  private UserAccessSnapshot? loadedAccess;
  private (bool Classifiers, bool IncludeChildren, bool AllSubdivisions)? loadedScope;
  private bool busy, disposed;
  private readonly Guid panelId = Guid.NewGuid();
  private DateTime? updatedSince;
  private bool? lastMode;
  private bool skipNextReload;
  private string? error;
  private string? operationError;
  private readonly CancellationTokenSource lifetime = new();
  private Task? polling;
  private Task? manualRefresh;
  private readonly SemaphoreSlim refresh = new(1, 1);

  protected override async Task OnParametersSetAsync()
  {
    var scope = (Classifiers, IncludeChildren, AllSubdivisions);
    if (ReferenceEquals(Access, loadedAccess) && loadedScope == scope) return;
    loadedAccess = Access;
    loadedScope = scope;
    if (Access != null) await RefreshAsync();
  }

  protected override Task OnAfterRenderAsync(bool firstRender)
  {
    if (firstRender) polling = PollAsync();
    return Task.CompletedTask;
  }

  private async Task PollAsync()
  {
    try
    {
      using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
      while (await timer.WaitForNextTickAsync(lifetime.Token))
        await InvokeAsync(async () => { await RunPollingCycleAsync(); if (!disposed) StateHasChanged(); });
    }
    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
  }

  public Task RefreshAsync() => RefreshCoreAsync(manual: false);

  private async Task RunPollingCycleAsync()
  {
    var attemptRefreshRequested = await RefreshCoreAsync(manual: false);
    if (disposed || lifetime.IsCancellationRequested) return;
    await PollingCycleCompleted.InvokeAsync(attemptRefreshRequested);
  }

  protected Task RefreshManuallyAsync()
  {
    if (manualRefresh is { IsCompleted: false }) return manualRefresh;
    return manualRefresh = RefreshCoreAsync(manual: true);
  }

  private async Task<bool> RefreshCoreAsync(bool manual)
  {
    if (disposed || Access == null) return false;
    var ct = lifetime.Token;
    try { await refresh.WaitAsync(ct); }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { return false; }
    var attemptChanged = false;
    var refreshed = false;
    try
    {
      var result = await Service.GetOverviewAsync(Classifiers, IncludeChildren, ct, AllSubdivisions);
      if (disposed) return false;
      var previous = lastSuccessfulAttempt;
      var current = (result.LastAttempt?.Id, result.LastAttempt?.Status, result.LastAttempt?.Phase, result.LastAttempt?.CompletedAt);
      var completed = previous.HasValue && result.LastAttempt?.Status == SynchronizationStatus.Succeeded &&
        (previous.Value.Id != result.LastAttempt.Id || previous.Value.Status != SynchronizationStatus.Succeeded);
      attemptChanged = previous.HasValue && previous.Value != current;
      overview = result; error = null;
      lastSuccessfulAttempt = current;
      refreshed = true;
      var api = result.Settings.Mode == DataManagementMode.Api;
      if (lastMode != api) { lastMode = api; await ApiModeChanged.InvokeAsync(api); }
      if (completed && !skipNextReload) await Synchronized.InvokeAsync();
      skipNextReload = false;
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex)
    {
      overview = null; error = "Не удалось проверить актуальность данных.";
      Logger.LogError(ex, "Получение состояния синхронизации");
      if (lastMode != true) { lastMode = true; await ApiModeChanged.InvokeAsync(true); }
    }
    finally { refresh.Release(); }
    if (disposed || ct.IsCancellationRequested) return false;
    if (manual && ManualRefreshRequested.HasDelegate) await ManualRefreshRequested.InvokeAsync();
    else if (refreshed && attemptChanged) await LastAttemptChanged.InvokeAsync();
    else if (refreshed) await RefreshCompleted.InvokeAsync();
    return !manual && refreshed && attemptChanged && LastAttemptChanged.HasDelegate;
  }

  protected async Task RunAsync()
  {
    if (busy || disposed) return;
    busy = true;
    operationError = null;
    using var scope = Logger.BeginScope(new Dictionary<string, object> { ["SyncPanelId"] = panelId });
    Logger.LogInformation("Ручная синхронизация: нажат запуск в панели {SyncPanelId}", panelId);
    try
    {
      var run = await Service.RunAsync(lifetime.Token, Access?.IsRoot == true && updatedSince.HasValue ? DateOnly.FromDateTime(updatedSince.Value) : null, enqueue: true);
      Logger.LogInformation("Ручная синхронизация: получен результат {RunId}, статус {SyncStatus}, панель {SyncPanelId}, уничтожена {PanelDisposed}", run.Id, run.Status, panelId, disposed);
      if (disposed) return;
      Notifications.Notify(new NotificationMessage {
        Severity = run.Status is SynchronizationStatus.Succeeded or SynchronizationStatus.Queued ? NotificationSeverity.Success : NotificationSeverity.Error,
        Summary = "Синхронизация", Detail = run.Status == SynchronizationStatus.Queued ? "Запуск принят. Выполнение продолжится на сервере; страницу можно закрыть." : run.Status == SynchronizationStatus.Succeeded ?
          $"Добавлено: {run.Added}, изменено: {run.Updated}, архивировано: {run.Archived}, восстановлено: {run.Restored}." : run.Error ?? "Синхронизация не завершена.",
        Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
      });
      if (run.Status == SynchronizationStatus.Succeeded) { skipNextReload = true; await Synchronized.InvokeAsync(); }
    }
    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Ручной запуск синхронизации"); operationError = UserOperationErrors.Message(ex);
      Notifications.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Синхронизация", Detail = operationError,
        Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" });
    }
    finally { busy = false; if (!disposed) await RefreshAsync(); }
  }

  public static string Format(DateTime? utc) => utc.HasValue ?
    TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), TimeZoneInfo.Local).ToString("dd.MM.yyyy HH:mm:ss") : "—";
  public static string StatusText(SynchronizationStatus status) => status switch {
    SynchronizationStatus.Running => "выполняется", SynchronizationStatus.Succeeded => "успешно",
    SynchronizationStatus.Queued => "ожидает запуска", SynchronizationStatus.Cancelled => "отменена", SynchronizationStatus.Interrupted => "прервана", _ => "ошибка"
  };
  public static string PhaseText(string? phase) => phase switch {
    "Classifiers" => "Классификаторы", "Subdivisions" => "Подразделения", "Personnel" => "Персонал", "ExitEvents" => "События выхода персонала",
    "Fetch" => "Получение данных API", "Dependencies" => "Дозагрузка зависимостей", "Apply" => "Проверка и сохранение", _ => "Подготовка"
  };
  private static bool NeedsRecovery(SynchronizationSettings settings)
  {
    if (settings.RecoveryRequiredFrom.HasValue) return true;
    var at = new[] { settings.LastFullSynchronizedAt, settings.LastChangesSynchronizedAt }.Where(d => d.HasValue).Max();
    return at.HasValue && DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(at.Value, TimeZoneInfo.Local)) < DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
  }

  public async ValueTask DisposeAsync()
  {
    if (busy) Logger.LogWarning("Ручная синхронизация: панель {SyncPanelId} уничтожается во время запуска; отменяется токен страницы, причина {CancellationReason}", panelId, "PanelDisposed");
    disposed = true; await lifetime.CancelAsync();
    if (polling != null) await polling;
    lifetime.Dispose();
  }
}
