using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace Guard.Components.Pages.Administrator.OrganTypes
{
  public partial class Edit : IDisposable
  {
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected TooltipService TooltipService { get; set; } = default!;
    [Inject] protected ContextMenuService ContextMenuService { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IOrganTypeService OrganTypeService { get; set; } = default!;
    [Inject] protected ILogger<Edit> Logger { get; set; } = default!;

    [CascadingParameter] protected UserContext? UserContext { get; set; }

    /// <summary>
    /// Идентификатор редактируемой записи типа органа
    /// </summary>
    [Parameter] public int Id { get; set; }

    private readonly CancellationTokenSource _cts = new();

    protected bool isLoading = false;
    protected OrganType item = new();

    protected override async Task OnInitializedAsync()
    {
      try
      {
        isLoading = true;
        Logger.LogDebug("Инициализация диалогового окна редактирования типа органа {OrganTypeId}", Id);

        // Загрузка существующей записи по ID
        var loadedItem = await OrganTypeService.GetByIdAsync(Id, _cts.Token);
        if (loadedItem == null)
        {
          Logger.LogWarning("Тип органа с ID {OrganTypeId} не найден", Id);
          ShowErrorNotification("Запись не найдена в базе данных");
          DialogService.Close(null);
          return;
        }

        item = loadedItem;
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация диалогового окна редактирования типа органа {OrganTypeId} была отменена", Id);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации диалогового окна редактирования типа органа {OrganTypeId}", Id);
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
        Logger.LogInformation("Запуск обновления типа органа '{Name}' (ID: {OrganTypeId})", item.Name, item.Id);

        // Проверка уникальности кода типа органа с исключением текущей записи
        bool isCodeUnique = await OrganTypeService.IsCodeUniqueAsync(item.Code, item.Id, _cts.Token);
        if (!isCodeUnique)
        {
          ShowErrorNotification($"Тип органа с кодом '{item.Code}' уже существует.");
          return;
        }

        await OrganTypeService.UpdateAsync(item, _cts.Token);

        Logger.LogInformation("Тип органа '{Name}' (ID: {OrganTypeId}) успешно обновлен", item.Name, item.Id);
        DialogService.Close(true);
      }
      catch (OperationCanceledException)
      {
        Logger.LogWarning("Операция обновления типа органа '{Name}' была отменена пользователем", item.Name);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при обновлении типа органа '{Name}'", item.Name);
        ShowErrorNotification(ex.Message);
      }
      finally
      {
        isLoading = false;
      }
    }

    protected void HandleCancelButtonClick()
    {
      Logger.LogInformation("Пользователь отменил редактирование типа органа через кнопку 'Отмена'");
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