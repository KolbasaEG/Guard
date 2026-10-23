using Guard.Components.Library;
using Guard.Core.Services.Exports;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Identity;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace Guard.Components.Pages.Users
{
  public partial class Index : IDisposable
  {
    [Inject] protected BrowserTimeService Time { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected ILogger<Index> Logger { get; set; } = default!;
    [Inject] protected IUserListService Security { get; set; } = default!;

    protected IEnumerable<UserFilterFields> data = [];
    protected IEnumerable<UserDto> filteredData = [];
    protected RadzenDataGrid<UserDto> grid = default!;
    protected RadzenDataFilter<UserFilterFields> dataFilter = default!;

    private CancellationTokenSource _cts = new();
    private CancellationTokenSource? _loadDataCts;
    private ExportFilter? appliedFilter;
    private bool disposed;
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
      if (firstRender) { try { await Time.InitializeAsync(); await grid.Reload(); StateHasChanged(); } catch (Exception ex) { Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Часовой пояс пользователей"); ShowErrorNotification("Не удалось определить часовой пояс браузера."); } }
    }

    int count;
    [CascadingParameter] public UserAccessSnapshot? Access { get; set; }
    protected bool isLoading = false;
    protected DataViewMode currentMode = DataViewMode.Active;
    string pagingSummaryFormat = "Страница {0} из {1} (всего {2} записей)";

    protected override async Task OnInitializedAsync()
    {
      try
      {
        isLoading = true;
        await Task.CompletedTask;
      }
      catch (Exception ex)
      {
        Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Ошибка при инициализации страницы пользователей");
        ShowErrorNotification(Guard.Core.Services.UserOperationErrors.Message(ex));
      }
      finally
      {
        isLoading = false;
      }
    }

    protected async Task LoadData(LoadDataArgs args)
    {
      _loadDataCts?.Cancel();
      var request = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
      _loadDataCts = request;
      var ct = request.Token;
      var filter = appliedFilter;
      isLoading = true;

      try
      {
        var result = await Security.SearchUsersAsync(new UserSearchRequest(args.Skip ?? 0, args.Top ?? 20, args.OrderBy, filter), ct);
        if (disposed || !ReferenceEquals(_loadDataCts, request)) return;
        filteredData = result.Items;
        count = result.Count;
      }
      catch (OperationCanceledException)
      {
        // Игнорируем отмененные запросы
      }
      catch (Exception ex)
      {
        if (disposed || !ReferenceEquals(_loadDataCts, request)) return;
        filteredData = []; count = 0;
        Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Ошибка загрузки данных пользователей");
        ShowErrorNotification("Не удалось загрузить данные");
      }
      finally
      {
        if (ReferenceEquals(_loadDataCts, request)) { _loadDataCts = null; if (!disposed) isLoading = false; }
        request.Dispose();
      }
    }
    protected async Task ReloadAsunc()
    {
      await grid.Reload();
    }

    protected Task EditRoles(UserDto item) => DialogService.OpenAsync<RoleAssignments>("Роли пользователя",
      new Dictionary<string, object> { ["UserId"] = item.Id }, new DialogOptions { Width = "800px", ShowTitle = false, ContentCssClass = "guard-entity-dialog-content" });
    protected async Task AddUserAsync()
    {
      var result = await DialogService.OpenAsync<Add>("", null, new DialogOptions { Width = "650px", ShowTitle = false, ContentCssClass = "guard-entity-dialog-content" });
      if (Guard.Components.Library.Dialogs.EntityDialogResult.IsSuccess(result)) await grid.Reload();
    }
    protected async Task EditUserAsync(UserDto item)
    {
      if (disposed || Access?.Has(Permissions.Users.Manage) != true || item.IsLockedOut) return;
      var result = await DialogService.OpenAsync<Edit>("", new Dictionary<string, object> { ["UserId"] = item.Id },
        new DialogOptions { Width = "650px", ShowTitle = false, ContentCssClass = "guard-entity-dialog-content" });
      if (Guard.Components.Library.Dialogs.EntityDialogResult.IsSuccess(result) && !disposed) {
        ShowSuccessNotification("Данные пользователя обновлены.");
        await grid.Reload();
      }
    }
    protected async Task ToggleLockout(UserDto item)
    {
      if (await DialogService.Confirm(item.IsLockedOut ? "Снять блокировку?" : "Заблокировать пользователя?", "Блокировка",
        new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) != true) return;
      try {
        await Security.SetUserLockoutAsync(item.Id, !item.IsLockedOut, _cts.Token);
        ShowSuccessNotification(item.IsLockedOut ? "Пользователь разблокирован." : "Пользователь заблокирован.");
        await grid.Reload();
      } catch (Exception ex) { Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Блокировка пользователя"); ShowErrorNotification("Изменение блокировки недоступно."); }
    }
    protected async Task ResetPasswordAsync(UserDto item)
    {
      var result = await DialogService.OpenAsync<ResetPassword>("",
        new Dictionary<string, object> { ["UserId"] = item.Id },
        new DialogOptions { Width = "650px", ShowTitle = false, ContentCssClass = "guard-entity-dialog-content" });
      if (Guard.Components.Library.Dialogs.EntityDialogResult.IsSuccess(result)) { ShowSuccessNotification("Пароль сброшен. При следующем входе потребуется его смена."); await grid.Reload(); }
    }
    async Task ApplyFilter()
    {
      try { appliedFilter = UserListQuery.Capture(dataFilter, Time); await grid.FirstPage(true); }
      catch (ArgumentException) { ShowErrorNotification("Проверьте условия фильтра."); }
    }


    private void ShowSuccessNotification(string detail)
    {
      NotificationService.Notify(new NotificationMessage
      {
        Severity = NotificationSeverity.Success,
        Summary = "Информационное",
        Detail = detail,
        Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
      });
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
      if (disposed) return;
      disposed = true;
      _loadDataCts?.Cancel();
      _loadDataCts?.Dispose();
      _cts?.Cancel();
      _cts?.Dispose();
    }
  }
}
