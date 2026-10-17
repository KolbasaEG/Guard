using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;
using SyncStatus = Guard.Components.Pages.Synchronization.StatusPanel;

namespace Guard.Components.Pages.Synchronization;

public partial class Page : IDisposable
{
  [Inject] protected ISynchronizationService Service { get; set; } = default!;
  [Inject] protected NotificationService Notifications { get; set; } = default!;
  [Inject] protected ILogger<Page> Logger { get; set; } = default!;
  [CascadingParameter] public UserAccessSnapshot? Access { get; set; }
  public sealed class SettingsForm
  {
    public DataManagementMode Mode { get; set; }
    public bool Enabled { get; set; }
    public string Time { get; set; } = "02:00";
    public string ApiBaseUrl { get; set; } = "";
    public int ApiPageSize { get; set; } = 500;
    public long? ApiHeadSubdivisionId { get; set; }
    public string ApiUsername { get; set; } = "";
    public string ApiPassword { get; set; } = "";
    public bool CompleteSnapshotGuaranteed { get; set; }
  }
  private readonly object[] modes = [new { Text = "Локальное управление", Value = DataManagementMode.Local }, new { Text = "Управление из API", Value = DataManagementMode.Api }];
  private readonly CancellationTokenSource lifetime = new();
  private readonly SemaphoreSlim reload = new(1, 1);
  private SyncStatus? statusPanel;
  private SettingsForm form = new();
  private SynchronizationSettingsDto? settings;
  private SynchronizationRunDto? last;
  private IReadOnlyList<SynchronizationRunDto> history = [];
  private bool busy, disposed;
  private int selectedTab;
  private string? error;
  private string? settingsError;
  private string? runsError;
  private bool settingsLoading, runsLoading, settingsNeedReload, savedSettingsNeedReload;
  private bool runsLoaded;
  private Task? runsReload;
  private bool runsReloadRequested;
  private string? connectionResult;
  private UserAccessSnapshot? loadedAccess;
  private bool ApiSourceChanged => settings != null &&
    SynchronizationSourceRules.SourceChanged(settings.ApiBaseUrl, settings.ApiUsername, form.ApiBaseUrl, form.ApiUsername);

  protected override async Task OnParametersSetAsync()
  {
    var initialize = Access?.IsRoot == true &&
      (loadedAccess?.IsRoot != true || loadedAccess.UserId != Access.UserId);
    loadedAccess = Access;
    if (!initialize) return;
    await ReloadSettingsAsync();
    await ReloadRunsAsync();
  }

  private async Task<bool> ReloadSettingsAsync()
  {
    if (disposed || Access?.IsRoot != true || settingsLoading) return false;
    var ct = lifetime.Token;
    settingsLoading = true;
    var entered = false;
    try
    {
      await reload.WaitAsync(ct);
      entered = true;
      var overview = await Service.GetOverviewAsync(ct: ct);
      if (disposed) return false;
      settings = overview.Settings;
      form = new() { Mode = settings.Mode, Enabled = settings.ScheduleEnabled, Time = settings.DailyTime.ToString("HH:mm"), ApiBaseUrl = settings.ApiBaseUrl, ApiPageSize = settings.ApiPageSize, ApiUsername = settings.ApiUsername, ApiHeadSubdivisionId = settings.ApiHeadSubdivisionId, CompleteSnapshotGuaranteed = settings.CompleteSnapshotGuaranteed };
      settingsError = null;
      settingsNeedReload = savedSettingsNeedReload = false;
      return true;
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { return false; }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Загрузка настроек синхронизации");
      settingsNeedReload = true;
      settingsError = savedSettingsNeedReload
        ? "Настройки сохранены, но не удалось обновить форму. Повторите загрузку настроек перед следующим сохранением."
        : "Не удалось загрузить настройки. Повторите загрузку настроек.";
      return false;
    }
    finally
    {
      if (entered) reload.Release();
      settingsLoading = false;
    }
  }

  private async Task RetrySettingsAsync()
  {
    if (busy || settingsLoading) return;
    await ReloadSettingsAsync();
  }

  private Task ReloadRunsAsync()
  {
    if (runsReload is { IsCompleted: false })
    {
      runsReloadRequested = true;
      return runsReload;
    }
    return runsReload = ReloadRunsLoopAsync();
  }

  private async Task ReloadRunsLoopAsync()
  {
    do
    {
      runsReloadRequested = false;
      await LoadRunsAsync();
    } while (runsReloadRequested && !disposed);
  }

  private async Task LoadRunsAsync()
  {
    if (disposed || Access?.IsRoot != true) return;
    var ct = lifetime.Token;
    runsLoading = true;
    var entered = false;
    try
    {
      await reload.WaitAsync(ct);
      entered = true;
      var runs = await Service.GetHistoryAsync(ct);
      if (disposed) return;
      last = runs.FirstOrDefault();
      history = runs;
      runsLoaded = true;
      runsError = null;
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex)
    {
      Logger.LogError(ex, "Загрузка запусков синхронизации");
      last = null; history = [];
      runsLoaded = false;
      runsError = "Не удалось загрузить запуски. Повторите загрузку или дождитесь автоматического обновления.";
    }
    finally
    {
      if (entered) reload.Release();
      runsLoading = false;
    }
  }

  private Task RetryRunsIfNeededAsync() => runsError != null ? ReloadRunsAsync() : Task.CompletedTask;

  private Task OnPollingCycleCompletedAsync(bool attemptRefreshRequested) =>
    attemptRefreshRequested ? Task.CompletedTask : RetryRunsIfNeededAsync();

  private async Task SaveAsync()
  {
    if (busy || settingsLoading || settingsNeedReload || settings == null || disposed) return;
    var ct = lifetime.Token;
    busy = true; error = null;
    try
    {
      if (!TimeOnly.TryParseExact(form.Time, "HH:mm", out var time)) throw new ArgumentException("Укажите время в формате ЧЧ:ММ.");
      var sourceChanged = ApiSourceChanged;
      await Service.SaveSettingsAsync(form.Mode, form.Enabled, time, settings.Version, ct, form.ApiBaseUrl, form.ApiPageSize, form.ApiUsername, form.ApiPassword, form.ApiHeadSubdivisionId, form.CompleteSnapshotGuaranteed);
      if (disposed) return;
      form.ApiPassword = "";
      settingsNeedReload = savedSettingsNeedReload = true;
      connectionResult = null;
      Notifications.Notify(new NotificationMessage { Severity = NotificationSeverity.Success, Summary = "Синхронизация", Detail = sourceChanged
        ? "Настройки сохранены. Источник API изменён; подтвердите полноту выгрузки отдельно и сохраните настройки повторно."
        : "Настройки сохранены.",
        Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" });
      await ReloadSettingsAsync();
      if (!disposed && statusPanel != null) await statusPanel.RefreshAsync();
      await ReloadRunsAsync();
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex) { Logger.LogError(ex, "Сохранение настроек синхронизации"); error = UserOperationErrors.Message(ex); }
    finally { busy = false; }
  }
  private async Task CheckConnectionAsync()
  {
    if (busy || settingsLoading || disposed) return;
    var ct = lifetime.Token;
    busy = true; connectionResult = null; error = null;
    try { await Service.CheckConnectionAsync(ct); if (!disposed) connectionResult = "Подключение и Basic auth подтверждены (ping HTTP 200)."; }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex) { Logger.LogWarning("Ошибка проверки API, тип {ExceptionType}", ex.GetType().Name); error = "Не удалось проверить подключение. Проверьте сохранённый адрес, Basic auth и технический журнал."; }
    finally { busy = false; }
  }

  public void Dispose()
  {
    if (disposed) return;
    disposed = true;
    lifetime.Cancel();
    lifetime.Dispose();
  }
}
