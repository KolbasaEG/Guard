using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Extensions;
using Guard.Core.Services;
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
    [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] protected NotificationService NotificationService { get; set; } = default!;
    [Inject] protected DialogService DialogService { get; set; } = default!;
    [Inject] protected ILogger<Index> Logger { get; set; } = default!;
    [Inject] protected ISecurityService Security { get; set; } = default!;

    protected IEnumerable<ApplicationUser> data = default!;
    protected IEnumerable<UserDto> filteredData = default!;
    protected RadzenDataGrid<UserDto> grid = default!;
    protected RadzenDataFilter<ApplicationUser> dataFilter = default!;

    private CancellationTokenSource _cts = new();
    private CancellationTokenSource? _loadDataCts;

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
        ShowErrorNotification(ex.Message);
      }
      finally
      {
        isLoading = false;
      }
    }

    async Task LoadData(LoadDataArgs args)
    {
      _loadDataCts?.Cancel();
      _loadDataCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
      var ct = _loadDataCts.Token;
      isLoading = true;

      try
      {
        if (dataFilter?.Filters != null) NormalizeFilterDatesToUtc(dataFilter.Filters);
        var (items, totalCount) = await Security.QueryUsersAsync(async query =>
        {
          if (dataFilter != null)
          {
            query = query.Where(dataFilter);
          }

          if (!string.IsNullOrEmpty(args.OrderBy))
          {
            query = query.OrderBy(args.OrderBy);
          }
          else
          {
            query = query.OrderByDescending(s => s.Id);
          }

          var total = await query.CountAsync(ct);

          var pageData = await query
              .Skip(args.Skip ?? 0)
              .Take(args.Top ?? 10)
              .Select(p => new UserDto
              {
                Id = p.Id,
                Email = p.Email ?? "-",
                IsLockedOut = p.LockoutEnd > DateTimeOffset.UtcNow, // Или p.IsLockedOut
                PersonalId = p.PersonalId,
                UserName = p.UserName ?? "-",
                PersonalFullName = p.Personal != null ? p.Personal.FullName : "-"
              })
              .ToListAsync(ct);

          return (pageData, total);
        }, ct);

        filteredData = items.ConvertDateTimesToLocal();
        count = totalCount;
      }
      catch (OperationCanceledException)
      {
        // Игнорируем отмененные запросы
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Ошибка загрузки данных пользователей");
        ShowErrorNotification("Не удалось загрузить данные");
      }
      finally
      {
        isLoading = false;
      }
    }
    protected async Task ReloadAsunc()
    {
      await grid.Reload();
    }

    protected Task EditRoles(UserDto item) => DialogService.OpenAsync<RoleAssignments>("Роли пользователя",
      new Dictionary<string, object> { ["UserId"] = item.Id }, new DialogOptions { Width = "800px" });
    protected async Task ToggleLockout(UserDto item)
    {
      if (await DialogService.Confirm(item.IsLockedOut ? "Снять блокировку?" : "Заблокировать пользователя?", "Блокировка") != true) return;
      try {
        var result = await Security.ToggleUserLockoutAsync(item.Id, !item.IsLockedOut, _cts.Token);
        if (!result.Succeeded) throw new InvalidOperationException("Не удалось изменить блокировку.");
        await grid.Reload();
      } catch (Exception ex) { Logger.LogWarning(ex, "Блокировка пользователя"); ShowErrorNotification("Изменение блокировки недоступно."); }
    }
    async Task ApplyFilter()
    {
      await grid.Reload();
    }

    private void NormalizeFilterDatesToUtc(IEnumerable<CompositeFilterDescriptor> filters)
    {
      if (filters == null) return;

      foreach (var filter in filters)
      {
        if (filter.FilterValue is DateTime dt && dt.Kind != DateTimeKind.Utc)
        {
          filter.FilterValue = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        if (filter.Filters != null && filter.Filters.Any())
        {
          NormalizeFilterDatesToUtc(filter.Filters);
        }
      }
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
      _loadDataCts?.Cancel();
      _loadDataCts?.Dispose();
      _cts?.Cancel();
      _cts?.Dispose();
    }
  }
}
