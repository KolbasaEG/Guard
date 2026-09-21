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
  public partial class Index : IDisposable
  {
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
      finalSelectedItemsLevel = selectedItemsLevel;
      finalSelectedItemsLayer = selectedItemsLayer;

      // Принудительно запускаем перерисовку, чтобы FilterValue передался в RadzenDataFilterProperty
      StateHasChanged();
      await Task.Yield();

      await dataFilter.Filter();
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
      isLoading = true;

      try
      {
        var (items, totalCount) = await LogService.GetLogsAsync(async query =>
        {
          if (dataFilter != null)
          {
            query = query.Where(dataFilter);
          }

          if (!string.IsNullOrEmpty(args.OrderBy))
          {
            query = query.OrderBy(args.OrderBy);
          }
          else
          {
            query = query.OrderByDescending(s => s.Timestamp);
          }

          var total = await query.CountAsync(_cts.Token);

          var pageData = await query
              .Skip(args.Skip ?? 0)
              .Take(args.Top ?? 10)
              .ToListAsync(_cts.Token);

          return (pageData, total);
        }, _cts.Token);

        filteredData = items;
        count = totalCount;
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Выборка системных логов отменена пользователем");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при загрузке системных логов");

        NotificationService.Notify(new NotificationMessage
        {
          Severity = NotificationSeverity.Error,
          Summary = "Внимание!",
          Detail = ex.Message,
          Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
        });
      }
      finally
      {
        isLoading = false;
      }
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

      _ = Task.Run(async () =>
      {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        try
        {
          while (await timer.WaitForNextTickAsync(token))
          {
            // RadzenDataGrid и UI обновляются только в потоке SynchronizationContext Blazor
            await InvokeAsync(async () =>
            {
              if (grid != null && !isLoading)
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
      }, token);
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
    protected void SaveLoggerSettings()
    {
      if (maxSessions < 1)
      {
        NotificationService.Notify(new NotificationMessage
        {
          Severity = NotificationSeverity.Warning,
          Summary = "Внимание",
          Detail = "Количество подключений должно быть не менее 1",
          Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
        });
        return;
      }

      var previousLevel = LevelSwitch.MinimumLevel;

      // 1. Обновляем уровень в switch
      LevelSwitch.MinimumLevel = selectedLogLevel;

      // 2. Применяем новую конфигурацию Serilog (батчи + уровень)
      LoggerManager.ApplyConfiguration(maxSessions, selectedLogLevel);

      // 3. Сохраняем обновленные настройки в файл logsettings.json
      LogLevelPersistenceService.SaveState(LevelSwitch, maxSessions);

      // 4. Логируем действие администратора
      Logger.LogWarning("Изменены настройки логгера: Уровень = {NewLevel} (был {PreviousLevel}), Макс. сессий = {MaxSessions}",
          selectedLogLevel, previousLevel, maxSessions);

      // 5. Показываем всплывающее уведомление
      NotificationService.Notify(new NotificationMessage
      {
        Severity = NotificationSeverity.Success,
        Summary = "Успешно",
        Detail = $"Настройки применены: {selectedLogLevel}, {maxSessions} сессий",
        Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
      });
    }

    /// <summary>
    /// Освобождение ресурсов при закрытии страницы
    /// </summary>
    public void Dispose()
    {
      StopRealtimeLoop();
      _cts.Cancel();
      _cts.Dispose();
    }
  }
}