using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.Workspaces.Administrator;

public partial class Synchronization : IDisposable
{
  [Inject] protected ISynchronizationService Service { get; set; } = default!;
  [Inject] protected NotificationService Notifications { get; set; } = default!;
  [Inject] protected ILogger<Synchronization> Logger { get; set; } = default!;
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
  }
  private readonly object[] modes = [new { Text = "Локальное управление", Value = DataManagementMode.Local }, new { Text = "Управление из API", Value = DataManagementMode.Api }];
  private readonly CancellationTokenSource lifetime = new();
  private SettingsForm form = new();
  private SynchronizationSettings? settings;
  private SynchronizationRun? last;
  private IReadOnlyList<SynchronizationRun> history = [];
  private bool busy, disposed;
  private string? error;
  private string? connectionResult;
  private UserAccessSnapshot? loadedAccess;

  protected override async Task OnParametersSetAsync()
  {
    if (ReferenceEquals(Access, loadedAccess)) return;
    loadedAccess = Access;
    if (Access?.IsRoot == true) await ReloadAsync();
  }

  private async Task ReloadAsync()
  {
    try
    {
      var overview = await Service.GetOverviewAsync(ct: lifetime.Token);
      if (disposed) return;
      settings = overview.Settings; last = overview.LastAttempt;
      history = await Service.GetHistoryAsync(lifetime.Token);
      form = new() { Mode = settings.Mode, Enabled = settings.ScheduleEnabled, Time = settings.DailyTime.ToString("HH:mm"), ApiBaseUrl = settings.ApiBaseUrl, ApiPageSize = settings.ApiPageSize, ApiUsername = settings.ApiUsername, ApiHeadSubdivisionId = settings.ApiHeadSubdivisionId };
    }
    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    catch (Exception ex) { Logger.LogError(ex, "Загрузка настроек синхронизации"); error = UserOperationErrors.Message(ex); }
  }

  private async Task SaveAsync()
  {
    if (busy || settings == null) return;
    busy = true; error = null;
    try
    {
      if (!TimeOnly.TryParseExact(form.Time, "HH:mm", out var time)) throw new ArgumentException("Укажите время в формате ЧЧ:ММ.");
      await Service.SaveSettingsAsync(form.Mode, form.Enabled, time, settings.Version, lifetime.Token, form.ApiBaseUrl, form.ApiPageSize, form.ApiUsername, form.ApiPassword, form.ApiHeadSubdivisionId);
      form.ApiPassword = "";
      Notifications.Notify(new NotificationMessage { Severity = NotificationSeverity.Success, Summary = "Синхронизация", Detail = "Настройки сохранены.",
        Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" });
      await ReloadAsync();
    }
    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    catch (Exception ex) { Logger.LogError(ex, "Сохранение настроек синхронизации"); error = UserOperationErrors.Message(ex); }
    finally { busy = false; }
  }

  private async Task CheckConnectionAsync()
  {
    if (busy) return;
    busy = true; connectionResult = null; error = null;
    try { await Service.CheckConnectionAsync(lifetime.Token); connectionResult = "Подключение и Basic auth подтверждены (ping HTTP 200)."; }
    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    catch (Exception ex) { Logger.LogWarning("Ошибка проверки API, тип {ExceptionType}", ex.GetType().Name); error = "Не удалось проверить подключение. Проверьте сохранённый адрес, Basic auth и технический журнал."; }
    finally { busy = false; }
  }

  public void Dispose() { disposed = true; lifetime.Cancel(); lifetime.Dispose(); }
}
