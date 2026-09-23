using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace Guard.Components.Pages.Administrator.Roles
{
  public partial class Edit : IDisposable
  {
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected TooltipService TooltipService { get; set; } = default!;
    [Inject] protected ContextMenuService ContextMenuService { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IApplicationRoleService RoleService { get; set; } = default!;
    [Inject] protected ILogger<Edit> Logger { get; set; } = default!;

    /// <summary>
    /// Строковый идентификатор редактируемой роли
    /// </summary>
    [Parameter] public string Id { get; set; } = default!;

    private readonly CancellationTokenSource _cts = new();

    protected bool isLoading = false;
    protected ApplicationRole item = new();

    protected override async Task OnInitializedAsync()
    {
      try
      {
        isLoading = true;
        Logger.LogDebug("Инициализация диалогового окна редактирования роли {RoleId}", Id);

        // Загрузка существующей записи по ID
        var loadedItem = await RoleService.GetByIdAsync(Id, _cts.Token);
        if (loadedItem == null)
        {
          Logger.LogWarning("Роль с ID {RoleId} не найдена", Id);
          ShowErrorNotification("Запись не найдена в базе данных");
          DialogService.Close(null);
          return;
        }

        item = loadedItem;
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация диалогового окна редактирования роли {RoleId} была отменена", Id);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации диалогового окна редактирования роли {RoleId}", Id);
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
        Logger.LogInformation("Запуск обновления роли '{RoleName}' (ID: {RoleId})", item.Name, item.Id);

        // Проверка уникальности имени с исключением текущего ID
        var isUnique = await RoleService.IsRoleNameUniqueAsync(item.Name, excludeId: item.Id, ct: _cts.Token);
        if (!isUnique)
        {
          ShowErrorNotification($"Роль с наименованием '{item.Name}' уже существует");
          return;
        }

        await RoleService.UpdateAsync(item, _cts.Token);

        Logger.LogInformation("Роль '{RoleName}' (ID: {RoleId}) успешно обновлена", item.Name, item.Id);
        DialogService.Close(true);
      }
      catch (OperationCanceledException)
      {
        Logger.LogWarning("Операция обновления роли '{RoleName}' была отменена пользователем", item.Name);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при обновлении роли '{RoleName}'", item.Name);
        ShowErrorNotification(ex.Message);
      }
      finally
      {
        isLoading = false;
      }
    }

    protected void HandleCancelButtonClick()
    {
      Logger.LogInformation("Пользователь отменил редактирование роли через кнопку 'Отмена'");
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