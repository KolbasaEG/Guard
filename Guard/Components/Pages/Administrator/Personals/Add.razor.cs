using Guard.Components.Pages.Workspaces.Administrator;
using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;

namespace Guard.Components.Pages.Administrator.Personals
{
  public partial class Add : IDisposable
  {
    [Inject]
    protected DialogService DialogService { get; set; } = default!;

    [Inject]
    protected NotificationService NotificationService { get; set; } = default!;

    [Inject]
    protected IPersonalService PersonalService { get; set; } = default!;

    [Inject]
    protected ILogger<Add> Logger { get; set; } = default!;

    private readonly CancellationTokenSource _cts = new();

    protected bool isLoading = false;
    protected Personal item = new();
    protected IEnumerable<Subdivision> subdivisions = [];

    protected override async Task OnInitializedAsync()
    {
      try
      {
        isLoading = true;
        Logger.LogDebug("Инициализация диалогового окна создания сотрудника");
        subdivisions = await PersonalService.GetAllActiveSubdivisionsAsync(_cts.Token);
        // Имитация загрузки (например, справочников/классификаторов)
        await Task.CompletedTask;
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация диалогового окна создания сотрудника была отменена");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации диалогового окна создания сотрудника");

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
      var displayName = GetDisplayName();

      try
      {
        isLoading = true;
        Logger.LogInformation("Запуск создания сотрудника '{PersonalName}'", displayName);

        // Асинхронное создание с передачей CancellationToken
        await PersonalService.CreateAsync(item, _cts.Token);

        Logger.LogInformation("Сотрудник '{PersonalName}' успешно создан", displayName);
        DialogService.Close(true);
      }
      catch (OperationCanceledException)
      {
        Logger.LogWarning("Операция создания сотрудника '{PersonalName}' была отменена пользователем", displayName);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при создании сотрудника '{PersonalName}'", displayName);

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
      Logger.LogInformation("Пользователь отменил создание сотрудника через кнопку 'Отмена'");
      _cts.Cancel();
      DialogService.Close(null);
    }

    private string GetDisplayName()
    {
      if (!string.IsNullOrWhiteSpace(item.FullName))
        return item.FullName;

      var name = $"{item.LastName} {item.FirstName} {item.MiddleName}".Trim();
      return string.IsNullOrWhiteSpace(name) ? "Новый сотрудник" : name;
    }

    public void Dispose()
    {
      _cts.Cancel();
      _cts.Dispose();
    }
  }
}