using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace Guard.Components.Pages.Administrator.IpAddresses
{
  public partial class Edit : IDisposable
  {
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected TooltipService TooltipService { get; set; } = default!;
    [Inject] protected ContextMenuService ContextMenuService { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IIpAddressService IpAddressService { get; set; } = default!;
    [Inject] protected ISubdivisionService SubdivisionService { get; set; } = default!;
    [Inject] protected ILogger<Edit> Logger { get; set; } = default!;

    [CascadingParameter] protected UserContext? UserContext { get; set; }

    /// <summary>
    /// Идентификатор редактируемой записи IP-адреса
    /// </summary>
    [Parameter] public Guid Id { get; set; }

    private readonly CancellationTokenSource _cts = new();

    protected bool isLoading = false;
    protected IpAddress item = new();
    protected IEnumerable<Subdivision> subdivisions = Enumerable.Empty<Subdivision>();

    protected override async Task OnInitializedAsync()
    {
      try
      {
        isLoading = true;
        Logger.LogDebug("Инициализация диалогового окна редактирования IP-адреса {IpAddressId}", Id);

        subdivisions = UserContext?.SubordinateSubdivisions ?? [];

        // Загрузка существующей записи по ID
        var loadedItem = await IpAddressService.GetByIdAsync(Id, _cts.Token);
        if (loadedItem == null)
        {
          Logger.LogWarning("IP-адрес с ID {IpAddressId} не найден", Id);
          ShowErrorNotification("Запись не найдена в базе данных");
          DialogService.Close(null);
          return;
        }

        item = loadedItem;
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация диалогового окна редактирования IP-адреса {IpAddressId} была отменена", Id);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации диалогового окна редактирования IP-адреса {IpAddressId}", Id);
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
        Logger.LogInformation("Запуск обновления IP-адреса '{IpAddress}' (ID: {IpAddressId})", item.Address, item.Id);

        await IpAddressService.UpdateAsync(item, _cts.Token);

        Logger.LogInformation("IP-адрес '{IpAddress}' (ID: {IpAddressId}) успешно обновлен", item.Address, item.Id);
        DialogService.Close(true);
      }
      catch (OperationCanceledException)
      {
        Logger.LogWarning("Операция обновления IP-адреса '{IpAddress}' была отменена пользователем", item.Address);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при обновлении IP-адреса '{IpAddress}'", item.Address);
        ShowErrorNotification(ex.Message);
      }
      finally
      {
        isLoading = false;
      }
    }

    protected void HandleCancelButtonClick()
    {
      Logger.LogInformation("Пользователь отменил редактирование IP-адреса через кнопку 'Отмена'");
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
    protected async Task InfoButtonClick()
    {
      var baseEntity = item as BaseEntity;
      await DialogService.OpenAsync<DialogInfo>("", new Dictionary<string, object> { { "BaseEntity", baseEntity } }, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
    }
    public void Dispose()
    {
      _cts.Cancel();
      _cts.Dispose();
    }
  }
}