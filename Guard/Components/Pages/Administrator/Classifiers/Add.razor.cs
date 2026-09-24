using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace Guard.Components.Pages.Administrator.Classifiers
{
  public partial class Add : IDisposable
  {
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected TooltipService TooltipService { get; set; } = default!;
    [Inject] protected ContextMenuService ContextMenuService { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IClassifierService ClassifierService { get; set; } = default!;
    [Inject] protected ILogger<Add> Logger { get; set; } = default!;

    [CascadingParameter] protected UserContext? UserContext { get; set; }

    private readonly CancellationTokenSource _cts = new();

    protected bool isLoading = false;
    protected Classifier item = new();

    protected override async Task OnInitializedAsync()
    {
      try
      {
        isLoading = true;
        Logger.LogDebug("Инициализация диалогового окна создания элемента классификатора");
        await Task.CompletedTask;
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация диалогового окна создания элемента классификатора была отменена");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации диалогового окна создания элемента классификатора");
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
        Logger.LogInformation("Запуск создания элемента классификатора '{Value}' (Type: {Type}, Code: {Code})", item.Value, item.Type, item.Code);

        bool isUnique = await ClassifierService.IsCodeUniqueInTypeAsync(item.Type, item.Code, null, _cts.Token);
        if (!isUnique)
        {
          ShowErrorNotification($"Код {item.Code} уже используется для типа справочника {item.Type}.");
          return;
        }

        await ClassifierService.CreateAsync(item, _cts.Token);

        Logger.LogInformation("Элемент классификатора '{Value}' успешно создан", item.Value);
        DialogService.Close(true);
      }
      catch (OperationCanceledException)
      {
        Logger.LogWarning("Операция создания элемента классификатора '{Value}' была отменена пользователем", item.Value);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при создании элемента классификатора '{Value}'", item.Value);
        ShowErrorNotification(ex.Message);
      }
      finally
      {
        isLoading = false;
      }
    }

    protected void HandleCancelButtonClick()
    {
      Logger.LogInformation("Пользователь отменил создание элемента классификатора через кнопку 'Отмена'");
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