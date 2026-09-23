using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;

namespace Guard.Components.Pages.Administrator.IpAddresses
{
  public partial class Add : IDisposable
  {
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected TooltipService TooltipService { get; set; } = default!;
    [Inject] protected ContextMenuService ContextMenuService { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected IIpAddressService IpAddressService { get; set; } = default!;
    [Inject] protected ISubdivisionService SubdivisionService { get; set; } = default!;
    [Inject] protected ILogger<Add> Logger { get; set; } = default!;
    [CascadingParameter] protected UserContext? UserContext { get; set; }

    private readonly CancellationTokenSource _cts = new();

    protected bool isLoading = false;
    protected IpAddress item = new();
    protected IEnumerable<Subdivision> subdivisions = Enumerable.Empty<Subdivision>();

    protected override async Task OnInitializedAsync()
    {
      try
      {
        isLoading = true;
        Logger.LogDebug("Инициализация диалогового окна создания IP-адреса");

        subdivisions = UserContext?.SubordinateSubdivisions ?? [];
      }
      catch (OperationCanceledException)
      {
        Logger.LogInformation("Инициализация диалогового окна создания IP-адреса была отменена");
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при инициализации диалогового окна создания IP-адреса");
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
        Logger.LogInformation("Запуск создания IP-адреса '{IpAddress}'", item.Address);

        await IpAddressService.CreateAsync(item, _cts.Token);

        Logger.LogInformation("IP-адрес '{IpAddress}' успешно создан", item.Address);
        DialogService.Close(true);
      }
      catch (OperationCanceledException)
      {
        Logger.LogWarning("Операция создания IP-адреса '{IpAddress}' была отменена пользователем", item.Address);
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка при создании IP-адреса '{IpAddress}'", item.Address);
        ShowErrorNotification(ex.Message);
      }
      finally
      {
        isLoading = false;
      }
    }

    protected void HandleCancelButtonClick()
    {
      Logger.LogInformation("Пользователь отменил создание IP-адреса через кнопку 'Отмена'");
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