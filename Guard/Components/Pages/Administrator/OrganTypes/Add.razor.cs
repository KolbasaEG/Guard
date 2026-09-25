using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace Guard.Components.Pages.Administrator.OrganTypes
{
  public partial class Add : IDisposable
  {
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected TooltipService TooltipService { get; set; } = default!;
    [Inject] protected ContextMenuService ContextMenuService { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IOrganTypeService OrganTypeService { get; set; } = default!;
    [Inject] protected ILogger<Add> Logger { get; set; } = default!;
    [Inject] protected ISubdivisionService SubdivisionService { get; set; }

    [CascadingParameter] protected UserContext? UserContext { get; set; }

    private readonly CancellationTokenSource _cts = new();

    protected bool isLoading = false;
    protected OrganType item = new();
    protected IEnumerable<Classifier> statusClassifiers = [];


    protected override async Task OnInitializedAsync()
    {
      try
      {
        isLoading = true;
        Logger.LogDebug("Инициализация диалогового окна создания типа органа");
        var statusClassifiersTask = SubdivisionService.GetClassifiersByTypeAsync(Core.Enums.ClassifierType.СтатусПодразделения, _cts.Token);
        await Task.WhenAll(statusClassifiersTask);
        statusClassifiers = await statusClassifiersTask;

        await Task.CompletedTask;
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация диалогового окна создания типа органа была отменена");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации диалогового окна создания типа органа");
        ShowErrorNotification(ex.Message);
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
        Logger.LogInformation("Запуск создания типа органа '{Name}' (Код: {Code})", item.Name, item.Code);

        // Проверка уникальности кода типа органа в классификаторе
        bool isCodeUnique = await OrganTypeService.IsCodeUniqueAsync(item.Code, null, _cts.Token);
        if (!isCodeUnique)
        {
          ShowErrorNotification($"Тип органа с кодом '{item.Code}' уже существует.");
          return;
        }

        await OrganTypeService.CreateAsync(item, _cts.Token);

        Logger.LogInformation("Тип органа '{Name}' успешно создан", item.Name);
        DialogService.Close(true);
      }
      catch (OperationCanceledException)
      {
        Logger.LogWarning("Операция создания типа органа '{Name}' была отменена пользователем", item.Name);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при создании типа органа '{Name}'", item.Name);
        ShowErrorNotification(ex.Message);
      }
      finally
      {
        isLoading = false;
      }
    }
    protected void OnStatusClassifierChanged(object value)
    {
      if (value is Classifier selected)
      {
        item.ClassifierType = selected.Type;
        item.Code = selected.Code;
      }
    }
    protected void HandleCancelButtonClick()
    {
      Logger.LogInformation("Пользователь отменил создание типа органа через кнопку 'Отмена'");
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