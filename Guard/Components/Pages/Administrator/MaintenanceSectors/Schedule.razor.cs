using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Radzen;

namespace Guard.Components.Pages.Administrator.MaintenanceSectors
{
  public partial class Schedule : IDisposable
  {
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected TooltipService TooltipService { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IMaintenanceSectorService SectorService { get; set; } = default!;
    [Inject] protected ILogger<Schedule> Logger { get; set; } = default!;

    [CascadingParameter] protected UserContext? UserContext { get; set; }

    private readonly CancellationTokenSource _cts = new();

    protected bool isLoading = false;
    protected bool isSaving = false;

    // Фильтры
    protected Guid? selectedSubdivisionId;
    protected DateTime? selectedMonth = DateTime.Today;
    protected IEnumerable<Subdivision> subdivisions = [];

    // Дни выбранного месяца
    protected List<DateTime> monthDays = new();

    // Список строк для отображения матрицы графика
    protected List<SectorScheduleRow> scheduleRows = new();

    protected override async Task OnInitializedAsync()
    {
      try
      {
        isLoading = true;
        subdivisions = UserContext?.SubordinateSubdivisions ?? [];

        if (subdivisions.Any())
        {
          selectedSubdivisionId = subdivisions.First().Id;
        }

        GenerateMonthDays();
        await LoadScheduleDataAsync();
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации страницы графика участков");
        ShowNotification(NotificationSeverity.Error, "Ошибка", ex.Message);
      }
      finally
      {
        isLoading = false;
      }
    }

    /// <summary>
    /// Генерирует список дней для выбранного месяца и года
    /// </summary>
    private void GenerateMonthDays()
    {
      monthDays.Clear();
      var currentMonth = selectedMonth ?? DateTime.Today;
      int daysCount = DateTime.DaysInMonth(currentMonth.Year, currentMonth.Month);

      for (int day = 1; day <= daysCount; day++)
      {
        monthDays.Add(new DateTime(currentMonth.Year, currentMonth.Month, day));
      }
    }

    /// <summary>
    /// Загружает участки подразделения и формирует сетку графика
    /// </summary>
    protected async Task LoadScheduleDataAsync()
    {
      if (!selectedSubdivisionId.HasValue || selectedSubdivisionId.Value == Guid.Empty)
      {
        scheduleRows.Clear();
        return;
      }

      try
      {
        isLoading = true;
        var currentMonth = selectedMonth ?? DateTime.Today;
        Logger.LogDebug("Загрузка участков для формирования графика (SubdivisionId: {SubdivisionId}, Period: {Year}-{Month})",
            selectedSubdivisionId, currentMonth.Year, currentMonth.Month);

        // Получаем участки с задействованными шаблонами недельной работы и ответственным
        var sectors = await SectorService.QuerySectorsAsync(query => query
            .Include(s => s.WeeklyPatterns)
            .Include(s => s.ResponsiblePersonal)
            .Where(s => s.SubdivisionId == selectedSubdivisionId.Value)
            .OrderBy(s => s.Name)
            .ToListAsync(_cts.Token), _cts.Token);

        scheduleRows = sectors.Select(sector =>
        {
          var row = new SectorScheduleRow
          {
            SectorId = sector.Id,
            SectorName = sector.Name,
            ResponsibleName = sector.ResponsiblePersonal != null
                ? $"{sector.ResponsiblePersonal.LastName} {sector.ResponsiblePersonal.FirstName}".Trim()
                : "Не назначен"
          };

          // Карта недельного шаблона участка
          var patternsDict = sector.WeeklyPatterns?.ToDictionary(p => p.DayOfWeek)
                             ?? new Dictionary<DayOfWeek, MaintenanceSectorWeeklyPattern>();

          foreach (var date in monthDays)
          {
            patternsDict.TryGetValue(date.DayOfWeek, out var pattern);

            row.Days[date] = new DayScheduleCell
            {
              Date = date,
              IsWorkDay = pattern?.IsWorkDay ?? (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday),
              WorkStart = pattern?.WorkStart ?? (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday ? new TimeSpan(8, 0, 0) : null),
              WorkEnd = pattern?.WorkEnd ?? (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday ? new TimeSpan(17, 0, 0) : null)
            };
          }

          return row;
        }).ToList();
      }
      catch (OperationCanceledException)
      {
        // Игнорируем отмену
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при загрузке данных графика участков");
        ShowNotification(NotificationSeverity.Error, "Ошибка", "Не удалось загрузить данные участков.");
      }
      finally
      {
        isLoading = false;
      }
    }

    /// <summary>
    /// Смена подразделения
    /// </summary>
    protected async Task OnSubdivisionChanged(Guid? value)
    {
      selectedSubdivisionId = value;
      await LoadScheduleDataAsync();
    }

    /// <summary>
    /// Смена месяца / года
    /// </summary>
    protected async Task OnMonthChanged(DateTime? value)
    {
      if (value.HasValue)
      {
        selectedMonth = value.Value;
        GenerateMonthDays();
        await LoadScheduleDataAsync();
      }
    }

    /// <summary>
    /// Автоматическое перезаполнение всего месяца строго по недельному шаблону (WeeklyPatterns)
    /// </summary>
    protected async Task AutoFillFromPatterns()
    {
      bool? confirm = await DialogService.Confirm(
          "Вы уверены, что хотите перерасчитать весь график месяца на основе недельных шаблонов участков?",
          "Сформировать график",
          new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" });

      if (confirm == true)
      {
        await LoadScheduleDataAsync();
        ShowNotification(NotificationSeverity.Success, "Успешно", "График сгенерирован по недельному шаблону.");
      }
    }

    /// <summary>
    /// Быстрое переключение статуса ячейки дня (Рабочий / Выходной)
    /// </summary>
    protected void ToggleDayStatus(DayScheduleCell cell)
    {
      cell.IsWorkDay = !cell.IsWorkDay;
      if (cell.IsWorkDay)
      {
        cell.WorkStart ??= new TimeSpan(8, 0, 0);
        cell.WorkEnd ??= new TimeSpan(17, 0, 0);
      }
      else
      {
        cell.WorkStart = null;
        cell.WorkEnd = null;
      }
      cell.IsModified = true;
    }

    /// <summary>
    /// Возвращает сокращённое наименование дня недели на русском
    /// </summary>
    protected string GetShortDayName(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
      DayOfWeek.Monday => "Пн",
      DayOfWeek.Tuesday => "Вт",
      DayOfWeek.Wednesday => "Ср",
      DayOfWeek.Thursday => "Чт",
      DayOfWeek.Friday => "Пт",
      DayOfWeek.Saturday => "Сб",
      DayOfWeek.Sunday => "Вс",
      _ => ""
    };

    /// <summary>
    /// Проверяет, является ли день выходным
    /// </summary>
    protected bool IsWeekend(DayOfWeek dayOfWeek) => dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday;

    /// <summary>
    /// Сохранение сгенерированного графика
    /// </summary>
    protected async Task SaveScheduleAsync()
    {
      try
      {
        isSaving = true;
        await Task.Delay(500); // Имитация запроса сохранения

        ShowNotification(NotificationSeverity.Success, "Сохранено", "График работы участков успешно сохранён.");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при сохранении графика работы участков");
        ShowNotification(NotificationSeverity.Error, "Ошибка", "Не удалось сохранить график.");
      }
      finally
      {
        isSaving = false;
      }
    }

    private void ShowNotification(NotificationSeverity severity, string summary, string detail)
    {
      NotificationService.Notify(new NotificationMessage
      {
        Severity = severity,
        Summary = summary,
        Detail = detail,
        Duration = 4000
      });
    }

    public void Dispose()
    {
      _cts.Cancel();
      _cts.Dispose();
    }

    #region Вспомогательные классы DTO для матрицы графика

    public class SectorScheduleRow
    {
      public Guid SectorId { get; set; }
      public string SectorName { get; set; } = string.Empty;
      public string ResponsibleName { get; set; } = string.Empty;
      public Dictionary<DateTime, DayScheduleCell> Days { get; set; } = new();
    }

    public class DayScheduleCell
    {
      public DateTime Date { get; set; }
      public bool IsWorkDay { get; set; }
      public TimeSpan? WorkStart { get; set; }
      public TimeSpan? WorkEnd { get; set; }
      public bool IsModified { get; set; }
    }

    #endregion
  }
}