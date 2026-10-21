using Guard.Components.Library;
using Guard.Core.Services.DTOs;
using Guard.Core.Services.Exports;
using Guard.Core.Enums;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace Guard.Components.Pages.Logs
{
  public partial class Index : IAsyncDisposable
  {
    [Inject] protected BrowserTimeService Time { get; set; } = default!;

    private ExportFilter? appliedFilter;
    private CancellationTokenSource? loading;
    private Task? realtimeTask;
    private readonly List<Task> realtimeTasks = [];
    private bool disposed;
    private bool reloadPending = true;
    private UserAccessSnapshot? loadedAccess;
    [CascadingParameter] public UserAccessSnapshot? Access { get; set; }
    protected override void OnParametersSet()
    {
      if (ReferenceEquals(loadedAccess, Access)) return;
      loadedAccess = Access; loading?.Cancel(); filteredData = []; count = 0; reloadPending = true;
    }
    protected override async Task OnAfterRenderAsync(bool firstRender) {
      if (disposed || !reloadPending) return;
      reloadPending = false;
      try { await Time.InitializeAsync(); await grid.Reload(); if (!disposed) StateHasChanged(); }
      catch (Exception ex) { Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Часовой пояс логов"); }
    }
    [Inject] protected IPermissionService Permissions { get; set; } = default!;
    [Inject]
    protected IJSRuntime JSRuntime { get; set; }
    [Inject]
    protected NotificationService NotificationService { get; set; }
    [Inject]
    protected DialogService DialogService { get; set; }
    [Inject]
    protected ILogSearchService LogService { get; set; }
    [Inject]
    protected ILogger<Index> Logger { get; set; }

    private readonly CancellationTokenSource _cts = new();
    private CancellationTokenSource? _realtimeCts;

    protected int count;
    protected int maxSessions = 100;
    protected bool isLoading = false;
    protected bool isRealtime = false;
    protected ApplicationLogLevel selectedLogLevel = ApplicationLogLevel.Information;
    protected bool settingsLoaded;
    protected bool settingsSaving;
    protected string pagingSummaryFormat = "Страница {0} из {1} (всего {2} записей)";
    

    protected IEnumerable<LogFilterFields> data = [];
    protected IEnumerable<LogEntryDto> filteredData = [];
    protected RadzenDataGrid<LogEntryDto> grid;
    protected RadzenDataFilter<LogFilterFields> dataFilter;
    

    private static readonly FilterOperator[] selectionOperators = [FilterOperator.In, FilterOperator.NotIn];

    protected IEnumerable<string> itemsLevel = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];
    protected IEnumerable<string> itemsLayer = ["Components", "Service", "Database", "System"];

    private BadgeStyle GetBadgeStyle(string? level) => level switch
    {
      "Error" or "Fatal" => BadgeStyle.Danger,
      "Warning" => BadgeStyle.Warning,
      "Information" => BadgeStyle.Info,
      "Debug" => BadgeStyle.Secondary,
      _ => BadgeStyle.Light
    };
    private BadgeStyle GetLayerBadgeStyle(string? layer) => layer switch
    {
      "Components" => BadgeStyle.Secondary,
      "Service" => BadgeStyle.Success,
      "Database" => BadgeStyle.Warning,
      _ => BadgeStyle.Light
    };

    // Применение фильтра
    async Task ApplyFilter()
    {
      try { appliedFilter = AdministrationListQuery.Capture(dataFilter, Time, LogSearchFilter.Fields, LogSearchFilter.Validate); if (grid.CurrentPage == 0) await grid.Reload(); else await grid.FirstPage(true); }
      catch (Exception ex) { Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Фильтр логов"); NotificationService.Notify(new NotificationMessage { Severity=NotificationSeverity.Error, Summary="Фильтр", Detail=Guard.Core.Services.UserOperationErrors.Message(ex), Style="position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" }); }
    }
    protected override async Task OnInitializedAsync()
    {
      if (disposed) return;
      settingsLoaded = false;
      try
      {
        isLoading = true;
        Logger.LogDebug("Инициализация панели управления системными логами");
        var savedState = await Configuration.GetAsync(_cts.Token);
        if (disposed) return;
        maxSessions = savedState.MaxSessions;
        selectedLogLevel = savedState.MinimumLevel;
        settingsLoaded = true;
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация панели логов была отменена");
      }
      catch (Exception ex)
      {
        if (disposed) return;
        Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Ошибка при инициализации панели логов");

        NotificationService.Notify(new NotificationMessage
        {
          Severity = NotificationSeverity.Error,
          Summary = "Ошибка!",
          Detail = "Не удалось загрузить настройки логирования. Обновите страницу и повторите попытку.",
          Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
        });
      }
      finally
      {
        isLoading = false;
      }
    }

    protected async Task LoadData(LoadDataArgs args)
    {
      if (disposed || Time.Zone == null) return;
      loading?.Cancel(); var request = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token); loading = request;
      var ct = request.Token; var filter = appliedFilter; isLoading = true;
      try {
        var result = await LogService.SearchAsync(new(args.Skip ?? 0, args.Top ?? 100, args.OrderBy, filter), ct);
        ct.ThrowIfCancellationRequested(); await Permissions.RequireAsync(Guard.Core.Identity.Permissions.Logs.Read, ct);
        if (!disposed && ReferenceEquals(request, loading)) { filteredData = result.Items; count = result.Count; }
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
      catch (Exception ex) { if (!disposed && ReferenceEquals(request, loading)) { filteredData = []; count = 0; Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Загрузка логов"); NotificationService.Notify(new NotificationMessage { Severity=NotificationSeverity.Error, Summary="Логи", Detail="Не удалось загрузить логи. Проверьте доступ и повторите попытку.", Style="position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" }); } }
      finally { if (ReferenceEquals(request, loading)) { loading=null; isLoading=false; } request.Dispose(); }
    }
    /// <summary>
    /// Переключение между режимами "По запросу" и "Real-time"
    /// </summary>
    private async Task OnRealtimeModeChanged(bool enabled)
    {
      isRealtime = enabled;

      if (isRealtime)
      {
        StartRealtimeLoop();
      }
      else
      {
        StopRealtimeLoop();
      }

      await Task.CompletedTask;
    }

    /// <summary>
    /// Запуск фоновой периодической перезагрузки реестра
    /// </summary>
    private void StartRealtimeLoop()
    {
      StopRealtimeLoop(); // Гарантируем остановку предыдущего цикла

      _realtimeCts = new CancellationTokenSource();
      var token = _realtimeCts.Token;

      realtimeTask = RunRealtimeAsync(token);
      realtimeTasks.RemoveAll(task => task.IsCompleted);
      realtimeTasks.Add(realtimeTask);
    }
    private async Task RunRealtimeAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        try
        {
          while (await timer.WaitForNextTickAsync(token))
          {
            // RadzenDataGrid и UI обновляются только в потоке SynchronizationContext Blazor
            await InvokeAsync(async () =>
            {
              if (!disposed && grid != null && !isLoading)
              {
                await grid.Reload();
                StateHasChanged();
              }
            });
          }
        }
        catch (OperationCanceledException)
        {
          // Ожидаемая отмена при переключении режима или уничтожении компонента
        }
        catch (Exception ex) { Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Автообновление логов остановлено"); }
    }

    /// <summary>
    /// Остановка фонового таймера
    /// </summary>
    private void StopRealtimeLoop()
    {
      if (_realtimeCts != null)
      {
        _realtimeCts.Cancel();
        _realtimeCts.Dispose();
        _realtimeCts = null;
      }
    }

    protected async Task EditRow(LogEntryDto item)
    {
      var result = await DialogService.OpenAsync<LogDetailsDialog>("", new Dictionary<string, object> { { "Log", item } }, new DialogOptions() { Width = "1200px", ShowTitle = false, ContentCssClass = "rz-p-1" });
    }

    /// <summary>
    /// Единый метод сохранения и применения настроек логирования
    /// </summary>
    [Inject] protected ILogConfigurationService Configuration { get; set; } = default!;
    protected async Task SaveLoggerSettings()
    {
      if (disposed || !settingsLoaded || settingsSaving) return;
      settingsSaving = true;
      var settings = new LogConfigurationDto(maxSessions, selectedLogLevel);
      try {
        var saved = await Configuration.SaveAsync(settings, _cts.Token);
        if (disposed) return;
        maxSessions = saved.MaxSessions;
        selectedLogLevel = saved.MinimumLevel;
        NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Success, Summary = "Успешно", Detail = "Настройки логирования сохранены.", Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" });
      }
      catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
      catch (Exception ex) { if (!disposed) { Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Настройки логирования"); NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Внимание", Detail = Guard.Core.Services.UserOperationErrors.Message(ex), Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" }); } }
      finally { settingsSaving = false; }
    }
    /// <summary>
    /// Освобождение ресурсов при закрытии страницы
    /// </summary>
    public async ValueTask DisposeAsync()
    {
      if (disposed) return;
      disposed = true;
      loading?.Cancel();
      StopRealtimeLoop();
      _cts.Cancel();
      await Task.WhenAll(realtimeTasks);
      _cts.Dispose();
    }
  }
}
