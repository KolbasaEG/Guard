using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace Guard.Components.Pages.Administrator.MaintenanceSectors
{
  public partial class Add : IDisposable
  {
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected TooltipService TooltipService { get; set; } = default!;
    [Inject] protected ContextMenuService ContextMenuService { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMaintenanceSectorService SectorService { get; set; } = default!;
    [Inject] protected IPersonalService PersonalService { get; set; } = default!;
    [Inject] protected IProtectedObjectService ProtectedObjectService { get; set; } = default!;
    [Inject] protected ILogger<Add> Logger { get; set; } = default!;

    [CascadingParameter] protected UserContext? UserContext { get; set; }

    private readonly CancellationTokenSource _cts = new();

    protected bool isLoading = false;
    protected MaintenanceSector item = new();
    protected IEnumerable<Subdivision> subdivisions = [];
    protected IEnumerable<Personal> personals = [];

    // Списки для работы RadzenPickList
    protected IEnumerable<ProtectedObject> availableObjects = [];
    protected IEnumerable<ProtectedObject> selectedObjects = [];

    // Шаблон недельного графика
    protected List<MaintenanceSectorWeeklyPattern> weeklyPatterns = new();

    protected override async Task OnInitializedAsync()
    {
      try
      {
        isLoading = true;
        Logger.LogDebug("Инициализация диалогового окна создания участка обслуживания");

        subdivisions = UserContext?.SubordinateSubdivisions ?? [];

        // Инициализация недельного шаблона (7 дней: Пн..Вс)
        InitializeWeeklyPatterns();
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация диалогового окна создания участка обслуживания была отменена");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации диалогового окна создания участка обслуживания");
        ShowErrorNotification(ex.Message);
      }
      finally
      {
        isLoading = false;
      }
    }

    /// <summary>
    /// Создаёт 7 дней недели с дефолтными значениями (Пн-Пт с 08:00 до 17:00, Сб-Вс — выходные)
    /// </summary>
    private void InitializeWeeklyPatterns()
    {
      var daysOrder = new[]
      {
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
      };

      weeklyPatterns = daysOrder.Select(day => new MaintenanceSectorWeeklyPattern
      {
        DayOfWeek = day,
        IsWorkDay = day != DayOfWeek.Saturday && day != DayOfWeek.Sunday,
        WorkStart = (day != DayOfWeek.Saturday && day != DayOfWeek.Sunday) ? new TimeSpan(8, 0, 0) : null,
        WorkEnd = (day != DayOfWeek.Saturday && day != DayOfWeek.Sunday) ? new TimeSpan(17, 0, 0) : null
      }).ToList();
    }

    /// <summary>
    /// Возвращает название дня недели на русском языке
    /// </summary>
    protected string GetDayOfWeekName(DayOfWeek day) => day switch
    {
      DayOfWeek.Monday => "Понедельник",
      DayOfWeek.Tuesday => "Вторник",
      DayOfWeek.Wednesday => "Среда",
      DayOfWeek.Thursday => "Четверг",
      DayOfWeek.Friday => "Пятница",
      DayOfWeek.Saturday => "Суббота",
      DayOfWeek.Sunday => "Воскресенье",
      _ => day.ToString()
    };

    /// <summary>
    /// Обработка переключения чекбокса рабочей смены
    /// </summary>
    protected void OnWorkDayChanged(MaintenanceSectorWeeklyPattern pattern, bool isWorkDay)
    {
      pattern.IsWorkDay = isWorkDay;
      if (isWorkDay)
      {
        pattern.WorkStart ??= new TimeSpan(8, 0, 0);
        pattern.WorkEnd ??= new TimeSpan(17, 0, 0);
      }
      else
      {
        pattern.WorkStart = null;
        pattern.WorkEnd = null;
      }
    }

    /// <summary>
    /// Обработчик смены выбранного подразделения
    /// </summary>
    protected async Task OnSubdivisionChanged(object value)
    {
      selectedObjects = [];
      personals = [];

      await LoadObjectsAsync();
    }

    /// <summary>
    /// Загружает объекты в зависимости от выбранного подразделения
    /// </summary>
    private async Task LoadObjectsAsync()
    {
      try
      {
        isLoading = true;
        if (item.SubdivisionId.HasValue && item.SubdivisionId.Value != Guid.Empty)
        {
          availableObjects = await ProtectedObjectService.GetBySubdivisionIdAsync(item.SubdivisionId.Value, _cts.Token);
          personals = await PersonalService.GetBySubdivisionIdAsync(item.SubdivisionId.Value, _cts.Token);
        }
        else
        {
          availableObjects = [];
          personals = [];
        }
      }
      catch (OperationCanceledException)
      {
        // Игнорируем отмену
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при загрузке охраняемых объектов персонала для подразделения {SubdivisionId}", item.SubdivisionId);
        ShowErrorNotification("Не удалось загрузить список объектов и персонала для выбранного подразделения.");
      }
      finally
      {
        isLoading = false;
      }
    }

    protected async Task FormSubmit()
    {
      try
      {
        isLoading = true;
        Logger.LogInformation("Запуск создания участка обслуживания '{SectorName}'", item.Name);

        bool isUnique = await SectorService.IsNameUniqueAsync(item.Name, item.SubdivisionId, ct: _cts.Token);
        if (!isUnique)
        {
          ShowErrorNotification($"Участок с наименованием '{item.Name}' уже существует в выбранном подразделении!");
          return;
        }

        // 1. Формирование связей для объектов участка
        item.SectorObjects = selectedObjects.Select(obj => new MaintenanceSectorObject
        {
          ProtectedObjectId = obj.Id,
          MaintenanceSectorId = item.Id
        }).ToList();

        // 2. Привязка недельного шаблона работы
        item.WeeklyPatterns = weeklyPatterns;

        await SectorService.CreateAsync(item, _cts.Token);

        Logger.LogInformation("Участок обслуживания '{SectorName}' успешно создан (ID: {SectorId})", item.Name, item.Id);
        DialogService.Close(true);
      }
      catch (OperationCanceledException)
      {
        Logger.LogWarning("Операция создания участка обслуживания '{SectorName}' была отменена пользователем", item.Name);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при создании участка обслуживания '{SectorName}'", item.Name);
        ShowErrorNotification(ex.Message);
      }
      finally
      {
        isLoading = false;
      }
    }

    protected void HandleCancelButtonClick()
    {
      Logger.LogInformation("Пользователь отменил создание участка обслуживания через кнопку 'Отмена'");
      _cts.Cancel();
      DialogService.Close(null);
    }

    private void ShowErrorNotification(string detail)
    {
      NotificationService.Notify(new NotificationMessage
      {
        Severity = NotificationSeverity.Error,
        Summary = "Внимание!",
        Detail = detail,
        Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
      });
    }

    public void Dispose()
    {
      _cts.Cancel();
      _cts.Dispose();
    }
  }
}