using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace Guard.Components.Pages.Administrator.Classifiers
{
  public partial class Edit : IDisposable
  {
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected TooltipService TooltipService { get; set; } = default!;
    [Inject] protected ContextMenuService ContextMenuService { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IClassifierService ClassifierService { get; set; } = default!;
    [Inject] protected ILogger<Edit> Logger { get; set; } = default!;

    [CascadingParameter] protected UserContext? UserContext { get; set; }

    /// <summary>
    /// Идентификатор редактируемой записи классификатора
    /// </summary>
    [Parameter] public int Id { get; set; }

    private readonly CancellationTokenSource _cts = new();

    protected bool isLoading = false;
    protected Classifier item = new();

    protected override async Task OnInitializedAsync()
    {
      try
      {
        isLoading = true;
        Logger.LogDebug("Инициализация диалогового окна редактирования элемента классификатора {ClassifierId}", Id);

        // Загрузка существующей записи по ID
        var loadedItem = await ClassifierService.GetByIdAsync(Id, _cts.Token);
        if (loadedItem == null)
        {
          Logger.LogWarning("Элемент классификатора с ID {ClassifierId} не найден", Id);
          ShowErrorNotification("Запись не найдена в базе данных");
          DialogService.Close(null);
          return;
        }

        item = loadedItem;
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация диалогового окна редактирования элемента классификатора {ClassifierId} была отменена", Id);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации диалогового окна редактирования элемента классификатора {ClassifierId}", Id);
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
        Logger.LogInformation("Запуск обновления элемента классификатора '{Value}' (ID: {ClassifierId})", item.Value, item.Id);

        bool isUnique = await ClassifierService.IsCodeUniqueInTypeAsync(item.Type, item.Code, item.Id, _cts.Token);
        if (!isUnique)
        {
          ShowErrorNotification($"Код {item.Code} уже используется для типа справочника {item.Type}.");
          return;
        }

        await ClassifierService.UpdateAsync(item, _cts.Token);

        Logger.LogInformation("Элемент классификатора '{Value}' (ID: {ClassifierId}) успешно обновлен", item.Value, item.Id);
        DialogService.Close(true);
      }
      catch (OperationCanceledException)
      {
        Logger.LogWarning("Операция обновления элемента классификатора '{Value}' была отменена пользователем", item.Value);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при обновлении элемента классификатора '{Value}'", item.Value);
        ShowErrorNotification(ex.Message);
      }
      finally
      {
        isLoading = false;
      }
    }

    protected void HandleCancelButtonClick()
    {
      Logger.LogInformation("Пользователь отменил редактирование элемента классификатора через кнопку 'Отмена'");
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