using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace Guard.Components.Pages.Administrator.Subdivisions
{
  public partial class Add : IDisposable
  {
    [Inject]
    protected IJSRuntime JSRuntime { get; set; }
    [Inject]
    protected NavigationManager NavigationManager { get; set; }
    [Inject]
    protected DialogService DialogService { get; set; }
    [Inject]
    protected TooltipService TooltipService { get; set; }
    [Inject]
    protected ContextMenuService ContextMenuService { get; set; }
    [Inject]
    protected NotificationService NotificationService { get; set; }
    [Inject]
    protected ISubdivisionService SubdivisionService { get; set; }
    [Inject]
    protected ILogger<Add> Logger { get; set; }

    private readonly CancellationTokenSource _cts = new();

    protected bool isLoading = false;
    protected Subdivision item = new();
    protected IEnumerable<Subdivision> subdivisions = [];
    protected IEnumerable<OrganType> organTypes = [];
    protected IEnumerable<Classifier> statusClassifiers = [];

    protected override async Task OnInitializedAsync()
    {
      try
      {
        isLoading = true;
        Logger.LogDebug("Инициализация диалогового окна создания подразделения");

        // Параллельная загрузка подразделений и справочников через SubdivisionService
        var subdivisionsTask = SubdivisionService.GetAllActiveAsync(_cts.Token);
        var organTypesTask = SubdivisionService.GetOrganTypesAsync(_cts.Token);
        var statusClassifiersTask = SubdivisionService.GetClassifiersByTypeAsync(Core.Enums.ClassifierType.СтатусПодразделения, _cts.Token);

        await Task.WhenAll(subdivisionsTask, organTypesTask, statusClassifiersTask);

        subdivisions = await subdivisionsTask;
        organTypes = await organTypesTask;
        statusClassifiers = await statusClassifiersTask;
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация диалогового окна создания подразделения была отменена");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации диалогового окна создания подразделения");

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
        Logger.LogInformation("Запуск создания подразделения '{SubdivisionName}'", item.Name);

        // Асинхронное создание с передачей CancellationToken
        await SubdivisionService.CreateAsync(item, _cts.Token);

        Logger.LogInformation("Подразделение '{SubdivisionName}' успешно создано", item.Name);
        DialogService.Close(true);
      }
      catch (OperationCanceledException)
      {
        Logger.LogWarning("Операция создания подразделения '{SubdivisionName}' была отменена пользователем", item.Name);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при создании подразделения '{SubdivisionName}'", item.Name);

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

    protected void HandleCancelButtonClick()
    {
      Logger.LogInformation("Пользователь отменил создание подразделения через кнопку 'Отмена'");
      _cts.Cancel();
      DialogService.Close(null);
    }
    protected void OnOrganTypeChanged(object value)
    {
      if (value is OrganType selected)
      {
        item.OrganTypeId = selected.Id;
        item.OrganTypeCode = selected.Code;
      }
      else
      {
        item.OrganTypeId = null;
        item.OrganTypeCode = null;
      }
    }

    protected void OnStatusClassifierChanged(object value)
    {
      if (value is Classifier selected)
      {
        item.StatusType = selected.Type;
        item.StatusCode = selected.Code;
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