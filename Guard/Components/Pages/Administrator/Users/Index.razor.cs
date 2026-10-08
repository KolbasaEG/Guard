using Guard.Components.Library;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Extensions;
using Guard.Core.Services;
using Guard.Core.Identity;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System.Linq.Dynamic.Core;

namespace Guard.Components.Pages.Administrator.Users
{
  public partial class Index : IDisposable
  {
    [Inject] protected BrowserTimeService Time { get; set; } = default!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected ILogger<Index> Logger { get; set; } = default!;
    [Inject] protected ISecurityService Security { get; set; } = default!;
    [Inject] protected IPermissionService PermissionsService { get; set; } = default!;
    [Inject] protected IAccountPolicyService Policies { get; set; } = default!;

    protected IEnumerable<ApplicationUser> data = [];
    protected IEnumerable<UserDto> filteredData = [];
    protected RadzenDataGrid<UserDto> grid = default!;
    protected RadzenDataFilter<ApplicationUser> dataFilter = default!;

    private CancellationTokenSource _cts = new();
    private CancellationTokenSource? _loadDataCts;
    private Func<IQueryable<ApplicationUser>, IQueryable<ApplicationUser>> appliedFilter = query => query;
    private bool disposed;
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
      if (firstRender) { try { await Time.InitializeAsync(); await grid.Reload(); StateHasChanged(); } catch (Exception ex) { Logger.LogWarning(ex, "Часовой пояс пользователей"); ShowErrorNotification("Не удалось определить часовой пояс браузера."); } }
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
        Logger.LogError(ex, "Ошибка при инициализации страницы пользователей");
        ShowErrorNotification(Guard.Core.Services.UserOperationErrors.Message(ex));
      }
      finally
      {
        isLoading = false;
      }
    }

    async Task LoadData(LoadDataArgs args)
    {
      _loadDataCts?.Cancel();
      var request = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
      _loadDataCts = request;
      var ct = request.Token;
      var filter = appliedFilter;
      isLoading = true;

      try
      {
        await PermissionsService.RequireAsync(Permissions.Users.Read, ct);
        var policy = await Policies.GetAsync(ct);
        var passwordCutoff = DateTimeOffset.UtcNow.AddDays(-policy.PasswordDays);
        var (items, totalCount) = await Security.QueryUsersAsync(async query =>
        {
          query = filter(query);

          var total = await query.CountAsync(ct);

          var pageData = await UserListQuery.Sort(query, args.OrderBy)
              .Skip(args.Skip ?? 0)
              .Take(args.Top ?? 10)
              .Select(p => new UserDto
              {
                Id = p.Id,
                Email = p.Email ?? "-",
                IsLockedOut = p.AccountBlockReason != null || p.LockoutEnabled && p.LockoutEnd > DateTimeOffset.UtcNow,
                AccountBlockReason = p.AccountBlockReason,
                LastActivityAtUtc = p.LastActivityAtUtc,
                PasswordChangedAtUtc = p.PasswordChangedAtUtc,
                MustChangePassword = p.MustChangePassword,
                PasswordChangeRequired = p.MustChangePassword || policy.PasswordExpirationEnabled &&
                  (p.PasswordChangedAtUtc != null ? p.PasswordChangedAtUtc <= passwordCutoff :
                    p.CreatedAtUtc <= passwordCutoff && (policy.PasswordEnabledAtUtc == null || policy.PasswordEnabledAtUtc <= passwordCutoff)),
                PersonalId = p.PersonalId,
                UserName = p.UserName ?? "-",
                PersonalFullName = p.Personal != null ? p.Personal.FullName : "-"
              })
              .ToListAsync(ct);

          return (pageData, total);
        }, ct);

        await PermissionsService.RequireAsync(Permissions.Users.Read, ct);
        if (disposed || !ReferenceEquals(_loadDataCts, request)) return;
        filteredData = items;
        count = totalCount;
      }
      catch (OperationCanceledException)
      {
        // Игнорируем отмененные запросы
      }
      catch (Exception ex)
      {
        if (disposed || !ReferenceEquals(_loadDataCts, request)) return;
        filteredData = []; count = 0;
        Logger.LogError(ex, "Ошибка загрузки данных пользователей");
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
      new Dictionary<string, object> { ["UserId"] = item.Id }, new DialogOptions { Width = "800px" });
    protected async Task AddUserAsync()
    {
      var result = await DialogService.OpenAsync<Add>("", null, new DialogOptions { Width = "650px", ShowTitle = false, ContentCssClass = "rz-p-1" });
      if (Guard.Components.Library.Dialogs.EntityDialogResult.IsSuccess(result)) await grid.Reload();
    }
    protected async Task ToggleLockout(UserDto item)
    {
      if (await DialogService.Confirm(item.IsLockedOut ? "Снять блокировку?" : "Заблокировать пользователя?", "Блокировка",
        new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) != true) return;
      try {
        var result = await Security.ToggleUserLockoutAsync(item.Id, !item.IsLockedOut, _cts.Token);
        if (!result.Succeeded) throw new InvalidOperationException("Не удалось изменить блокировку.");
        ShowSuccessNotification(item.IsLockedOut ? "Пользователь разблокирован." : "Пользователь заблокирован.");
        await grid.Reload();
      } catch (Exception ex) { Logger.LogWarning(ex, "Блокировка пользователя"); ShowErrorNotification("Изменение блокировки недоступно."); }
    }
    protected async Task ResetPasswordAsync(UserDto item)
    {
      var result = await DialogService.OpenAsync<ResetPassword>("",
        new Dictionary<string, object> { ["UserId"] = item.Id },
        new DialogOptions { Width = "650px", ShowTitle = false, ContentCssClass = "rz-p-1" });
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
      disposed = true;
      _loadDataCts?.Cancel();
      _loadDataCts?.Dispose();
      _cts?.Cancel();
      _cts?.Dispose();
    }
  }
}
