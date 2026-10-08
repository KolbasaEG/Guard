using Guard.Components.Library;
using Guard.Core.Services.DTOs;
using Guard.Core.Entities;
using Guard.Core.Extensions;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System.Linq.Dynamic.Core;

namespace Guard.Components.Pages.Administrator.Roles
{
  public partial class Index : IDisposable
  {
    [Inject] protected IPermissionService PermissionService { get; set; } = default!;
    [Inject] protected BrowserTimeService Time { get; set; } = default!;
    private static readonly HashSet<string> supportedFields = ["Id", "Name"];
    private Func<IQueryable<ApplicationRole>, IQueryable<ApplicationRole>> appliedFilter = q => q;
    private bool disposed, reloadPending = true;
    private Guard.Core.Services.DTOs.UserAccessSnapshot? loadedAccess;
    protected override void OnParametersSet() { if (!ReferenceEquals(loadedAccess, Access)) { loadedAccess = Access; _loadDataCts?.Cancel(); filteredData = []; count = 0; reloadPending = true; } }
    protected override async Task OnAfterRenderAsync(bool firstRender) {
      if (disposed || !reloadPending) return; reloadPending = false;
      try { await Time.InitializeAsync(); await grid.Reload(); }
      catch (Exception ex) { Logger.LogWarning(ex, "Загрузка часового пояса"); ShowErrorNotification("Не удалось определить часовой пояс браузера. Обновите страницу."); }
      if (!disposed) StateHasChanged();
    }
    private async Task ResetPageAsync() { if (grid.CurrentPage == 0) await grid.Reload(); else await grid.FirstPage(true); }

    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected ILogger<Index> Logger { get; set; } = default!;
    [Inject] protected IApplicationRoleService RoleService { get; set; } = default!;

    protected IEnumerable<ApplicationRole> data = [];
    protected IEnumerable<RoleListDto> filteredData = [];
    protected RadzenDataGrid<RoleListDto> grid = default!;
    protected RadzenDataFilter<ApplicationRole> dataFilter = default!;

    private readonly CancellationTokenSource _cts = new();
    private CancellationTokenSource? _loadDataCts;

    int count;
    [CascadingParameter] public Guard.Core.Services.DTOs.UserAccessSnapshot? Access { get; set; }
    protected bool isEditor => Access?.Has(Guard.Core.Identity.Permissions.Roles.Manage) == true;
    protected bool isLoading = false;
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
        Logger.LogError(ex, "Ошибка при инициализации страницы ролей");
        ShowErrorNotification(Guard.Core.Services.UserOperationErrors.Message(ex));
      }
      finally
      {
        isLoading = false;
      }
    }

    async Task LoadData(LoadDataArgs args)
    {
      if (disposed || Time.Zone == null) return;
      _loadDataCts?.Cancel();
      var request = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
      _loadDataCts = request; var ct = request.Token;
      var filter = appliedFilter;
      isLoading = true;
      try {
        if ((args.Skip ?? 0) < 0 || (args.Top ?? 20) is < 1 or > 100) throw new ArgumentException("Недопустимые параметры страницы.");
        var result = await RoleService.QueryRolesAsync(async query => {
          query = filter(query);
          var total = await query.CountAsync(ct);
          query = EntityListQuery<ApplicationRole>.Sort(query, args.OrderBy, supportedFields, "Name asc");
          var page = await query.Skip(args.Skip ?? 0).Take(args.Top ?? 20).Select(p => new RoleListDto(p.Id, p.Name, p.NormalizedName)).ToListAsync(ct);
          return (page, total);
        }, ct);
        ct.ThrowIfCancellationRequested();
        await PermissionService.RequireAsync(Guard.Core.Identity.Permissions.Roles.Read, ct);
        if (disposed || !ReferenceEquals(request, _loadDataCts)) return;
        filteredData = result.page; count = result.total;
      }
      catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
      catch (Exception ex) {
        if (!disposed && ReferenceEquals(request, _loadDataCts)) {
          filteredData = []; count = 0; Logger.LogError(ex, "Загрузка списка");
          ShowErrorNotification("Не удалось загрузить данные. Проверьте доступ и повторите попытку.");
        }
      }
      finally { if (ReferenceEquals(request, _loadDataCts)) { isLoading = false; _loadDataCts = null; } request.Dispose(); }
    }
    protected async Task ReloadAsync()
    {
      await grid.Reload();
    }

    protected async Task AddClick(MouseEventArgs args)
    {
      var result = await DialogService.OpenAsync<Add>("", null, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
      if (Guard.Components.Library.Dialogs.EntityDialogResult.IsSuccess(result))
      {
        ShowSuccessNotification("Добавлена новая запись!");
        await grid.Reload();
      }
    }

    protected async Task EditRow(RoleListDto item)
    {
      var result = await DialogService.OpenAsync<Edit>("", new Dictionary<string, object?> { { "Id", item.Id } }, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });

      if (Guard.Components.Library.Dialogs.EntityDialogResult.IsSuccess(result))
      {
        ShowSuccessNotification("Информация обновлена!");
        await grid.Reload();
      }
    }

    protected async Task GridDeleteButtonClick(MouseEventArgs args, RoleListDto item)
    {
      if (await DialogService.Confirm($"Вы действительно хотите удалить роль '{item.Name}'?", "Удаление роли", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          await RoleService.DeleteAsync(item.Id, ct: _cts.Token);
          ShowSuccessNotification("Роль успешно удалена!");
          await grid.Reload();
        }
        catch (Exception ex)
        {
          Logger.LogError(ex, "Ошибка при удалении роли {RoleId}", item.Id);
          ShowErrorNotification(Guard.Core.Services.UserOperationErrors.Message(ex));
        }
      }
    }



    async Task ApplyFilter() { try { appliedFilter = EntityListQuery<ApplicationRole>.Capture(dataFilter, supportedFields, Time); await ResetPageAsync(); } catch (Exception ex) { Logger.LogWarning(ex, "Фильтр списка"); ShowErrorNotification(ex is ArgumentException ? ex.Message : "Не удалось применить фильтр."); } }


    private void ShowSuccessNotification(string detail)
    {
      NotificationService.Notify(new NotificationMessage
      {
        Severity = NotificationSeverity.Success,
        Summary = "Успешно",
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

      _cts?.Cancel();
      _cts?.Dispose();
    }
  }
}
