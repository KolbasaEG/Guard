using Guard.ComponentLibrary.Loading;
using Guard.Components.Pages.Workspaces.Administrator;
using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen;

namespace Guard.Components.Pages.Administrator.Subdivisions
{
  public partial class Edit : IDisposable
  {
    [Inject]
    protected DialogService DialogService { get; set; }
    [Inject]
    protected NotificationService NotificationService { get; set; }
    [Inject]
    protected ISubdivisionService SubdivisionService { get; set; }
    [Inject]
    protected ILogger<Edit> Logger { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private readonly CancellationTokenSource _cts = new();

    protected bool isLoading = false;
    protected Subdivision item = new();
    protected OrganType? selectedOrganType;
    protected Classifier? selectedStatusClassifier;
    protected IEnumerable<OrganType> organTypes = [];
    protected IEnumerable<Classifier> statusClassifiers = [];

    protected override async Task OnInitializedAsync()
    {
      if (Id == Guid.Empty)
      {
        Logger.LogWarning("Передан пустой ID при вызове формы редактирования подразделения");
        DialogService.Close(null);
        return;
      }

      try
      {
        isLoading = true;
        Logger.LogDebug("Инициализация диалогового окна редактирования подразделения с ID '{Id}'", Id);

        var entity = await SubdivisionService.GetByIdAsync(Id, _cts.Token);
        if (entity != null)
        {
          // Параллельная загрузка справочников и списка подразделений
          var organTypesTask = SubdivisionService.GetOrganTypesAsync(_cts.Token);
          var statusClassifiersTask = SubdivisionService.GetClassifiersByTypeAsync(Core.Enums.ClassifierType.СтатусПодразделения, _cts.Token);

          await Task.WhenAll(organTypesTask, statusClassifiersTask);

          organTypes = await organTypesTask;
          statusClassifiers = await statusClassifiersTask;

          item = entity;

          // Инициализация выбранных объектов для корректного отображения в DropDown
          selectedOrganType = organTypes.FirstOrDefault(x => x.Id == item.OrganTypeId && x.Code == item.OrganTypeCode);
          selectedStatusClassifier = statusClassifiers.FirstOrDefault(x => x.Type == item.StatusType && x.Code == item.StatusCode);
        }
        else
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
        }
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация диалогового окна редактирования подразделения была отменена");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации диалогового окна редактирования подразделения с ID '{Id}'", Id);
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
        Logger.LogInformation("Запуск обновления подразделения '{SubdivisionName}' (ID: {Id})", item.Name, item.Id);

        // Асинхронное обновление сущности
        await SubdivisionService.UpdateAsync(item, _cts.Token);

        Logger.LogInformation("Подразделение '{SubdivisionName}' с ID '{Id}' успешно обновлено", item.Name, item.Id);
        DialogService.Close(true);
      }
      catch (OperationCanceledException)
      {
        Logger.LogWarning("Операция обновления подразделения '{SubdivisionName}' была отменена пользователем", item.Name);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при обновлении подразделения '{SubdivisionName}' (ID: {Id})", item.Name, item.Id);

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
      Logger.LogInformation("Пользователь отменил редактирование подразделения через кнопку 'Отмена'");
      _cts.Cancel();
      DialogService.Close(null);
    }

    protected void OnOrganTypeChanged(object value)
    {
      selectedOrganType = value as OrganType;
      if (selectedOrganType != null)
      {
        item.OrganTypeId = selectedOrganType.Id;
        item.OrganTypeCode = selectedOrganType.Code;
      }
      else
      {
        item.OrganTypeId = null;
        item.OrganTypeCode = null;
      }
    }

    protected void OnStatusClassifierChanged(object value)
    {
      selectedStatusClassifier = value as Classifier;
      if (selectedStatusClassifier != null)
      {
        item.StatusType = selectedStatusClassifier.Type;
        item.StatusCode = selectedStatusClassifier.Code;
      }
      else
      {
        item.StatusType = null;
        item.StatusCode = null;
      }
    }

    public void Dispose()
    {
      _cts.Cancel();
      _cts.Dispose();
    }
  }
}