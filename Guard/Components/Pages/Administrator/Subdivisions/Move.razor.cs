using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Shared;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages.Administrator.Subdivisions
{
  public partial class Move : IDisposable
  {
    [Inject]
    protected DialogService DialogService { get; set; } = default!;

    [Inject]
    protected NotificationService NotificationService { get; set; } = default!;

    [Inject]
    protected ISubdivisionService SubdivisionService { get; set; } = default!;

    [Inject]
    protected ILogger<Move> Logger { get; set; } = default!;

    [Parameter]
    public Guid Id { get; set; }

    private readonly CancellationTokenSource _cts = new();

    protected bool isLoading = false;
    protected Subdivision item = new();
    protected Guid? selectedParentId;
    protected IEnumerable<Subdivision> availableParents = new List<Subdivision>();

    protected override async Task OnInitializedAsync()
    {
      if (Id == Guid.Empty)
      {
        Logger.LogWarning("Передан пустой ID при вызове формы перемещения подразделения");
        DialogService.Close(null);
        return;
      }

      try
      {
        isLoading = true;
        Logger.LogDebug("Инициализация диалогового окна перемещения подразделения с ID '{Id}'", Id);

        var entity = await SubdivisionService.GetByIdAsync(Id, _cts.Token);
        if (entity == null)
        {
          Logger.LogWarning("Подразделение с ID '{Id}' не найдено", Id);
          NotificationService.Notify(new NotificationMessage
          {
            Severity = NotificationSeverity.Warning,
            Summary = "Внимание!",
            Detail = $"Подразделение с ID '{Id}' не найдено",
            Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
          });
          DialogService.Close(null);
          return;
        }

        item = entity;
        selectedParentId = item.ParentId;

        // Загрузка всех подразделений для построения списка доступных родителей
        var allSubdivisions = await SubdivisionService.GetAllActiveAsync(_cts.Token);

        // Исключаем перемещаемый узел и всех его потомков из списка допустимых родителей
        availableParents = allSubdivisions
            .Where(s => s.Id != item.Id && (string.IsNullOrEmpty(item.Path) || !s.Path.StartsWith(item.Path)))
            .OrderBy(s => s.Name)
            .ToList();
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация диалогового окна перемещения подразделения была отменена");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации диалогового окна перемещения подразделения с ID '{Id}'", Id);
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

    protected async Task FormSubmit()
    {
      try
      {
        isLoading = true;
        Logger.LogInformation("Запуск перемещения подразделения '{SubdivisionName}' (ID: {Id}) в новое родительское подразделение (ID: {NewParentId})",
            item.Name, item.Id, selectedParentId);

        // Асинхронный вызов бизнес-логики перемещения и каскадного пересчета путей потомков
        await SubdivisionService.MoveAsync(item.Id, selectedParentId, _cts.Token);

        Logger.LogInformation("Подразделение '{SubdivisionName}' с ID '{Id}' успешно перемещено", item.Name, item.Id);
        DialogService.Close(true);
      }
      catch (OperationCanceledException)
      {
        Logger.LogWarning("Операция перемещения подразделения '{SubdivisionName}' была отменена пользователем", item.Name);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при перемещении подразделения '{SubdivisionName}' (ID: {Id})", item.Name, item.Id);

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

    protected async Task InfoButtonClick()
    {
      var baseEntity = item as BaseEntity;
      await DialogService.OpenAsync<DialogInfo>("", new Dictionary<string, object> { { "BaseEntity", baseEntity } }, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
    }

    protected void HandleCancelButtonClick()
    {
      Logger.LogInformation("Пользователь отменил перемещение подразделения через кнопку 'Отмена'");
      _cts.Cancel();
      DialogService.Close(null);
    }

    public void Dispose()
    {
      _cts.Cancel();
      _cts.Dispose();
    }
  }
}