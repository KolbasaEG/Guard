using Guard.Components.Library;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Logging;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using Serilog.Core;
using Serilog.Events;
using System.Linq.Dynamic.Core;

namespace Guard.Components.Pages.Root.LogInfo
{
  public partial class Index : IAsyncDisposable
  {
    [Inject] protected BrowserTimeService Time { get; set; } = default!;
    private static readonly HashSet<string> supportedFields = ["Timestamp", "Level", "Layer", "Message", "Exception", "UserId", "ClientIp"];
    private Func<IQueryable<LogEntry>, IQueryable<LogEntry>> appliedFilter = q => q;
    private CancellationTokenSource? loading;
    private Task? realtimeTask;
    private readonly List<Task> realtimeTasks = [];
    private bool disposed;
    protected override async Task OnAfterRenderAsync(bool firstRender) {
      if (firstRender) { try { await Time.InitializeAsync(); await grid.Reload(); StateHasChanged(); } catch (Exception ex) { Logger.LogWarning(ex, "Часовой пояс логов"); } }
    }
    [Inject] protected IPermissionService Permissions { get; set; } = default!;
    [Inject]
    protected IJSRuntime JSRuntime { get; set; }
    [Inject]
    protected NotificationService NotificationService { get; set; }
    [Inject]
    protected DialogService DialogService { get; set; }
    [Inject]
    protected LoggingLevelSwitch LevelSwitch { get; set; }
    [Inject]
    protected ILogService LogService { get; set; }
    [Inject]
    protected ILogger<Index> Logger { get; set; }
    [Inject]
    protected DynamicLoggerManager LoggerManager { get; set; }

    private readonly CancellationTokenSource _cts = new();
    private CancellationTokenSource? _realtimeCts;


    protected int count;
    protected int maxSessions = 100;
    protected bool isLoading = false;
    protected bool isRealtime = false;
    protected LogEventLevel selectedLogLevel;
    protected string pagingSummaryFormat = "Страница {0} из {1} (всего {2} записей)";
    

    protected IEnumerable<LogEntry> data;
    protected IEnumerable<LogEntry> filteredData;
    protected RadzenDataGrid<LogEntry> grid;
    protected RadzenDataFilter<LogEntry> dataFilter;
    

    // Переменные для Level
    protected IEnumerable<string> itemsLevel = ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];
    protected IEnumerable<string> selectedItemsLevel;
    protected IEnumerable<string> finalSelectedItemsLevel;

    // Переменные для Layer
    protected IEnumerable<string> itemsLayer = ["Components", "Service", "Database", "System"];
    protected IEnumerable<string> selectedItemsLayer;
    protected IEnumerable<string> finalSelectedItemsLayer;

    // Обработчики изменений наборов для фильтра
    void OnSelectedLevelChange(object value)
    {
      if (selectedItemsLevel != null && !selectedItemsLevel.Any())
      {
        selectedItemsLevel = null;
      }
    }
    void OnSelectedLayerChange(object value)
    {
      if (selectedItemsLayer != null && !selectedItemsLayer.Any())
      {
        selectedItemsLayer = null;
      }
    }

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
      finalSelectedItemsLevel = selectedItemsLevel?.ToArray();
      finalSelectedItemsLayer = selectedItemsLayer?.ToArray();
      SetSelectionValues(dataFilter.Filters ?? []);
      try { appliedFilter = EntityListQuery<LogEntry>.Capture(dataFilter, supportedFields, Time); if (grid.CurrentPage == 0) await grid.Reload(); else await grid.FirstPage(true); }
      catch (Exception ex) { Logger.LogWarning(ex, "Фильтр логов"); NotificationService.Notify(new NotificationMessage { Severity=NotificationSeverity.Error, Summary="Фильтр", Detail=Guard.Core.Services.UserOperationErrors.Message(ex), Style="position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" }); }
    }
    private void SetSelectionValues(IEnumerable<CompositeFilterDescriptor> filters)
    {
      foreach (var filter in filters) {
        if (filter.Property == "Level") filter.FilterValue = finalSelectedItemsLevel;
        if (filter.Property == "Layer") filter.FilterValue = finalSelectedItemsLayer;
        if (filter.Filters != null) SetSelectionValues(filter.Filters);
      }
    }
    protected override async Task OnInitializedAsync()
    {
      try
      {
        isLoading = true;
        Logger.LogDebug("Инициализация панели управления системными логами");
        // Загружаем сохраненный уровень и количество сессий
        var savedState = LogLevelPersistenceService.LoadState();
        maxSessions = savedState.MaxSessions;
        selectedLogLevel = LevelSwitch.MinimumLevel;
        await Task.CompletedTask;
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация панели логов была отменена");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации панели логов");

        NotificationService.Notify(new NotificationMessage
        {
          Severity = NotificationSeverity.Error,
          Summary = "Ошибка!",
          Detail = "Не удалось инициализировать панель управления логами",
          Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
        });
      }
      finally
      {
        isLoading = false;
      }
    }

    async Task LoadData(LoadDataArgs args)
    {
      if (disposed || Time.Zone == null) return;
      loading?.Cancel(); var request = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token); loading = request;
      var ct = request.Token; var filter = appliedFilter; isLoading = true;
      try {
        if ((args.Skip ?? 0) < 0 || (args.Top ?? 100) is < 1 or > 100) throw new ArgumentException("Недопустимые параметры страницы.");
        var result = await LogService.GetLogsAsync(async query => {
          query = filter(query); var total = await query.CountAsync(ct);
          query = EntityListQuery<LogEntry>.Sort(query, args.OrderBy, supportedFields, "Timestamp desc", uniqueKey: null);
          var page = await query.Skip(args.Skip ?? 0).Take(args.Top ?? 100).ToListAsync(ct);
          return (page, total);
        }, ct);
        ct.ThrowIfCancellationRequested(); await Permissions.RequireAsync(Guard.Core.Identity.Permissions.Logs.Read, ct);
        if (!disposed && ReferenceEquals(request, loading)) { filteredData = result.page; count = result.total; }
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
      catch (Exception ex) { if (!disposed && ReferenceEquals(request, loading)) { filteredData = []; count = 0; Logger.LogError(ex, "Загрузка логов"); NotificationService.Notify(new NotificationMessage { Severity=NotificationSeverity.Error, Summary="Логи", Detail="Не удалось загрузить логи. Проверьте доступ и повторите попытку.", Style="position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" }); } }
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
        catch (Exception ex) { Logger.LogError(ex, "Автообновление логов остановлено"); }
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

    protected async Task EditRow(LogEntry item)
    {
      var result = await DialogService.OpenAsync<LogDetailsDialog>("", new Dictionary<string, object> { { "Log", item } }, new DialogOptions() { Width = "1200px", ShowTitle = false, ContentCssClass = "rz-p-1" });
    }

    /// <summary>
    /// Единый метод сохранения и применения настроек логирования
    /// </summary>
    [Inject] protected ILogConfigurationService Configuration { get; set; } = default!;
    protected async Task SaveLoggerSettings()
    {
      try {
        await Configuration.SaveAsync(maxSessions, selectedLogLevel, _cts.Token);
        NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Success, Summary = "Успешно", Detail = "Настройки логирования сохранены.", Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" });
      }
      catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
      catch (Exception ex) { Logger.LogError(ex, "Настройки логирования"); NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Внимание", Detail = Guard.Core.Services.UserOperationErrors.Message(ex), Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" }); }
    }
    /// <summary>
    /// Освобождение ресурсов при закрытии страницы
    /// </summary>
    public async ValueTask DisposeAsync()
    {
      disposed = true;
      loading?.Cancel();
      StopRealtimeLoop();
      _cts.Cancel();
      await Task.WhenAll(realtimeTasks);
      _cts.Dispose();
    }
  }
}
