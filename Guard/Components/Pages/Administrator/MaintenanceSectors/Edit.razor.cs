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
  public partial class Edit : IDisposable
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
    [Inject] protected ILogger<Edit> Logger { get; set; } = default!;

    [CascadingParameter] protected UserContext? UserContext { get; set; }

    /// <summary>
    /// Идентификатор редактируемого участка обслуживания
    /// </summary>
    [Parameter] public Guid Id { get; set; }

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
        Logger.LogDebug("Инициализация диалогового окна редактирования участка обслуживания {SectorId}", Id);

        subdivisions = UserContext?.SubordinateSubdivisions ?? [];

        // Загружаем участок вместе со связанными объектами и недельнольным шаблоном
        var loadedItem = await SectorService.QuerySectorsAsync(query => query
            .Include(s => s.SectorObjects)
                .ThenInclude(so => so.ProtectedObject)
            .Include(s => s.WeeklyPatterns)
            .FirstOrDefaultAsync(s => s.Id == Id, _cts.Token), _cts.Token);

        if (loadedItem == null)
        {
          Logger.LogWarning("Участок обслуживания с ID {SectorId} не найден", Id);
          ShowErrorNotification("Запись не найдена в базе данных");
          DialogService.Close(null);
          return;
        }

        item = loadedItem;

        // Инициализация недельного графика (7 дней)
        InitializeWeeklyPatterns(item.WeeklyPatterns);

        // Первоначальная загрузка объектов и персонала для подразделения участка
        if (item.SubdivisionId.HasValue && item.SubdivisionId.Value != Guid.Empty)
        {
          var currentSelectedIds = item.SectorObjects
              .Select(so => so.ProtectedObjectId)
              .ToHashSet();

          var allObjects = await ProtectedObjectService.GetBySubdivisionIdAsync(item.SubdivisionId.Value, _cts.Token);

          personals = await PersonalService.GetBySubdivisionIdAsync(item.SubdivisionId.Value, _cts.Token);
          selectedObjects = allObjects.Where(o => currentSelectedIds.Contains(o.Id)).ToList();
          availableObjects = allObjects.Where(o => !currentSelectedIds.Contains(o.Id)).ToList();
        }
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация диалогового окна редактирования участка обслуживания {SectorId} была отменена", Id);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации диалогового окна редактирования участка обслуживания {SectorId}", Id);
        ShowErrorNotification(ex.Message);
      }
      finally
      {
        isLoading = false;
      }
    }

    /// <summary>
    /// Инициализирует недельный график с восстановлением существующих записей или созданием дефолтных
    /// </summary>
    private void InitializeWeeklyPatterns(ICollection<MaintenanceSectorWeeklyPattern>? existingPatterns)
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

      var existingDict = existingPatterns?.ToDictionary(p => p.DayOfWeek)
                         ?? new Dictionary<DayOfWeek, MaintenanceSectorWeeklyPattern>();

      weeklyPatterns = daysOrder.Select(day =>
      {
        if (existingDict.TryGetValue(day, out var existing))
        {
          return existing;
        }

        return new MaintenanceSectorWeeklyPattern
        {
          MaintenanceSectorId = item.Id,
          DayOfWeek = day,
          IsWorkDay = day != DayOfWeek.Saturday && day != DayOfWeek.Sunday,
          WorkStart = (day != DayOfWeek.Saturday && day != DayOfWeek.Sunday) ? new TimeSpan(8, 0, 0) : null,
          WorkEnd = (day != DayOfWeek.Saturday && day != DayOfWeek.Sunday) ? new TimeSpan(17, 0, 0) : null
        };
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
      item.ResponsiblePersonalId = Guid.Empty;

      await LoadObjectsAndPersonalsAsync();
    }

    /// <summary>
    /// Загружает объекты и персонал при смене подразделения
    /// </summary>
    private async Task LoadObjectsAndPersonalsAsync()
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
        // Игнорируем отмену операции
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при загрузке объектов и персонала для подразделения {SubdivisionId}", item.SubdivisionId);
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
        Logger.LogInformation("Запуск обновления участка обслуживания '{SectorName}' (ID: {SectorId})", item.Name, item.Id);

        bool isUnique = await SectorService.IsNameUniqueAsync(item.Name, item.SubdivisionId, excludeId: item.Id, ct: _cts.Token);
        if (!isUnique)
        {
          ShowErrorNotification($"Участок с наименованием '{item.Name}' уже существует в выбранном подразделении!");
          return;
        }

        // 1. Обновляем коллекцию промежуточных связей объектов
        item.SectorObjects = selectedObjects.Select(obj => new MaintenanceSectorObject
        {
          ProtectedObjectId = obj.Id,
          MaintenanceSectorId = item.Id
        }).ToList();

        // 2. Обновляем недельный шаблон работы
        item.WeeklyPatterns = weeklyPatterns;

        await SectorService.UpdateAsync(item, _cts.Token);

        Logger.LogInformation("Участок обслуживания '{SectorName}' (ID: {SectorId}) успешно обновлен", item.Name, item.Id);
        DialogService.Close(true);
      }
      catch (OperationCanceledException)
      {
        Logger.LogWarning("Операция обновления участка обслуживания '{SectorName}' была отменена пользователем", item.Name);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при обновлении участка обслуживания '{SectorName}'", item.Name);
        ShowErrorNotification(ex.Message);
      }
      finally
      {
        isLoading = false;
      }
    }

    protected void HandleCancelButtonClick()
    {
      Logger.LogInformation("Пользователь отменил редактирование участка обслуживания через кнопку 'Отмена'");
      _cts.Cancel();
      DialogService.Close(null);
    }

    protected async Task InfoButtonClick()
    {
      var baseEntity = item as BaseEntity;
      await DialogService.OpenAsync<DialogInfo>("", new Dictionary<string, object> { { "BaseEntity", baseEntity } }, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
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