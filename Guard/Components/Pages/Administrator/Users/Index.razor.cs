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
    protected bool isEditor = true;
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
    protected async Task AddClick(MouseEventArgs args)
    {
      //var result = await DialogService.OpenAsync<Add>("", null, new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" });
      //if (result != null)
      //{
      //  ShowSuccessNotification("Добавлена новая запись!");
      //  await grid.Reload();
      //}
    }

    protected async Task EditRow(UserDto item)
    {
      //var result = await DialogService.OpenAsync<Edit>(
      //  "",
      //  new Dictionary<string, object?> { { "Id", item.Id } },
      //  new DialogOptions() { Width = "800px", ShowTitle = false, ContentCssClass = "rz-p-1" }
      //);

      //if (result != null)
      //{
      //  ShowSuccessNotification("Информация обновлена!");
      //  await grid.Reload();
      //}
    }

    protected async Task GridArchiveButtonClick(MouseEventArgs args, UserDto item)
    {
      if (await DialogService.Confirm("Вы действительно хотите поместить запись в архив?", "Архивирование", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          //await Security.ArchiveAsync(item.Id, ct: _cts.Token);
          ShowSuccessNotification("Запись помещена в архив!");
          await grid.Reload();
        }
        catch (Exception ex)
        {
          ShowErrorNotification(ex.Message);
        }
      }
    }

    protected async Task GridUnarchiveButtonClick(MouseEventArgs args, UserDto item)
    {
      if (await DialogService.Confirm("Вы действительно хотите извлечь запись из архива?", "Извлечение из архива", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          //await Security.RestoreAsync(item.Id, ct: _cts.Token);
          ShowSuccessNotification("Запись извлечена из архива!");
          await grid.Reload();
        }
        catch (Exception ex)
        {
          ShowErrorNotification(ex.Message);
        }
      }
    }

    protected async Task GridDeleteButtonClick(MouseEventArgs args, UserDto item)
    {
      if (await DialogService.Confirm("Вы действительно хотите удалить запись?", "Удаление", new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) == true)
      {
        try
        {
          //await Security.SoftDeleteAsync(item.Id, ct: _cts.Token);
          ShowSuccessNotification("Запись удалена!");
          await grid.Reload();
        }
        catch (Exception ex)
        {
          ShowErrorNotification(ex.Message);
        }
      }
    }

    protected async Task ReloadAsync()
    {
      await grid.Reload();
    }

    private async Task OnExportClick()
    {
      // TODO: Реализовать экспорт
      await Task.CompletedTask;
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