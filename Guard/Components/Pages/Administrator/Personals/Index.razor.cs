using Guard.Components.Library.Dialogs;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace Guard.Components.Pages.Administrator.Personals;

public partial class Index : IDisposable
{
  protected bool apiManaged = true;
  private readonly Guard.Components.Library.Loading.ListLoadConfirmation synchronizationLoad = new();
  protected Task<bool> ReloadForSynchronizationAsync() => synchronizationLoad.ReloadAsync(ReloadGridForSynchronizationAsync);
  protected virtual Task ReloadGridForSynchronizationAsync() { if (disposed || grid == null) return Task.CompletedTask; grid.ResetLoadData(); return grid.Reload(); }
  [Inject] protected IPersonalService Service { get; set; } = default!;
  [Inject] protected DialogService Dialogs { get; set; } = default!;
  [Inject] protected IJSRuntime JS { get; set; } = default!;
  [Inject] protected ILogger<Index> Logger { get; set; } = default!;
  [Inject] protected IPermissionService PermissionService { get; set; } = default!;
  [Inject] protected NotificationService Notifications { get; set; } = default!;
  [CascadingParameter] public UserAccessSnapshot? Access { get; set; }
  [CascadingParameter] public UserContext? UserContext { get; set; }

  protected IEnumerable<Personal> data = [];
  protected IEnumerable<PersonalListItemDto> filteredData = [];
  protected RadzenDataGrid<PersonalListItemDto> grid = default!;
  protected RadzenDataFilter<Personal> dataFilter = default!;
  protected int count;
  protected bool isLoading;
  protected string? error;
  protected DataViewMode mode = DataViewMode.Active;
  protected SubdivisionHierarchyMode hierarchy = SubdivisionHierarchyMode.CurrentOnly;
  protected bool allRecords;
  protected bool ShowAll => Access?.IsRoot == true && allRecords;
  protected const string PagingSummaryFormat = "Страница {0} из {1} (всего {2} записей)";

  private readonly CancellationTokenSource cts = new();
  private CancellationTokenSource? loading;
  private PersonalFilter? appliedFilter;
  private string? sort;
  private bool disposed, reloadPending = true, operationPending;
  private UserAccessSnapshot? loadedAccess;
  private UserContext? loadedContext;
  protected bool ActionsDisabled => isLoading || operationPending;

  protected override void OnParametersSet()
  {
    if (ReferenceEquals(loadedAccess, Access) && ReferenceEquals(loadedContext, UserContext)) return;
    if (Access?.IsRoot != true) allRecords = false;
    else if (loadedAccess?.IsRoot != true || loadedAccess.UserId != Access.UserId) allRecords = true;
    loadedAccess = Access;
    loadedContext = UserContext;
    loading?.Cancel();
    filteredData = [];
    count = 0;
    if (mode == DataViewMode.Archived && Access?.Has(Permissions.Personals.ReadArchive) != true ||
        mode == DataViewMode.Deleted && Access?.IsRoot != true)
      mode = DataViewMode.Active;
    reloadPending = true;
  }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (disposed || !reloadPending || grid == null || Access == null) return;
    reloadPending = false;
    await ResetPageAsync();
    if (!disposed) StateHasChanged();
  }

  private PersonalSearchRequest Request(int skip = 0, int take = 20) => new(
    Mode: mode, Skip: skip, Take: take, OrderBy: sort, Filter: appliedFilter,
    OwnSubdivision: !ShowAll, IncludeChildren: hierarchy == SubdivisionHierarchyMode.IncludeChildren, AllSubdivisions: ShowAll);

  protected async Task OnAllRecordsChanged(bool value)
  {
    allRecords = Access?.IsRoot == true && value;
    await ResetPageAsync();
  }

  protected bool CanWrite(PersonalListItemDto row) => !apiManaged && Access?.Has(Permissions.Personals.Write) == true &&
    (row.Status is not (Status.Archived or Status.ArchivedBlocked) || Access.Has(Permissions.Personals.ReadArchive));

  protected async Task LoadData(LoadDataArgs args)
  {
    if (disposed || Access == null) return;
    var loadTicket = synchronizationLoad.Begin();
    loading?.Cancel();
    var request = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
    loading = request;
    var ct = request.Token;
    var initialAccess = Access;
    isLoading = true;
    error = null;
    sort = args.OrderBy;
    var search = Request(args.Skip ?? 0, args.Top ?? 20);
    try
    {
      await PermissionService.RequireAsync(Permissions.Personals.Read, ct);
      var result = await Service.SearchAsync(search, ct);
      ct.ThrowIfCancellationRequested();
      await PermissionService.RequireAsync(Permissions.Personals.Read, ct);
      ct.ThrowIfCancellationRequested();
      if (!disposed && ReferenceEquals(loading, request) && ReferenceEquals(initialAccess, Access))
      {
        filteredData = result.Items;
        count = result.Count;
        synchronizationLoad.Confirm(loadTicket);
      }
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex)
    {
      if (!disposed && ReferenceEquals(loading, request))
      {
        filteredData = [];
        count = 0;
        ShowError(ex, "Загрузка списка сотрудников");
      }
    }
    finally
    {
      if (ReferenceEquals(loading, request)) { isLoading = false; loading = null; }
      request.Dispose();
    }
  }

  protected async Task ResetPageAsync()
  {
    if (disposed || grid == null) return;
    if (grid.CurrentPage == 0) await grid.Reload();
    else await grid.FirstPage(true);
  }

  protected async Task OnHierarchyModeChanged(bool includeChildren)
  {
    hierarchy = includeChildren ? SubdivisionHierarchyMode.IncludeChildren : SubdivisionHierarchyMode.CurrentOnly;
    await ResetPageAsync();
  }

  protected async Task ApplyFilter()
  {
    if (disposed || dataFilter == null) return;
    try
    {
      appliedFilter = CaptureFilter(dataFilter.Filters ?? [], dataFilter.LogicalFilterOperator);
      await ResetPageAsync();
    }
    catch (ArgumentException ex) { ShowError(ex, "Применение фильтра сотрудников"); }
  }

  protected static PersonalFilter CaptureFilter(IEnumerable<CompositeFilterDescriptor> filters, LogicalFilterOperator logicalOperator)
  {
    var nodes = 0;
    PersonalFilter Clone(CompositeFilterDescriptor filter, int depth)
    {
      if (depth > 16 || ++nodes > 100) throw new ArgumentException("Слишком сложный фильтр.");
      if (filter.FilterValue != null && filter.FilterValue is not string)
        throw new ArgumentException("Проверьте значение текстового фильтра.");
      var children = filter.Filters?.ToArray();
      return new(filter.Property, filter.FilterOperator.ToString(), filter.FilterValue as string,
        filter.LogicalFilterOperator == LogicalFilterOperator.And,
        children is { Length: > 0 } ? children.Select(child => Clone(child, depth + 1)).ToArray() : null);
    }
    var result = new PersonalFilter(All: logicalOperator == LogicalFilterOperator.And,
      Children: filters.Select(filter => Clone(filter, 1)).ToArray());
    // Строим выражение без запроса к БД: ошибочный фильтр не заменяет применённый.
    PersonalQueryFilter.Apply(Array.Empty<Personal>().AsQueryable(), result);
    return result;
  }

  protected Task ReloadAsync() => disposed || grid == null ? Task.CompletedTask : grid.Reload();

  private async Task OpenDialog<T>(Guid? id = null) where T : IComponent
  {
    if (disposed || operationPending) return;
    var args = id.HasValue ? new Dictionary<string, object?> { ["Id"] = id.Value } : null;
    var result = await Dialogs.OpenAsync<T>("", args,
      new DialogOptions { Width = "850px", ShowTitle = false, ContentCssClass = "rz-p-1" });
    await HandleResultAsync(result);
  }

  private async Task HandleResultAsync(object? result)
  {
    if (disposed || !EntityDialogResult.IsSuccess(result)) return;
    Notify(NotificationSeverity.Success, "Изменения сохранены.");
    await ReloadAsync();
  }

  protected Task OpenRow(PersonalListItemDto row) => Access?.Has(Permissions.Personals.ReadDetails) == true
    ? DetailsAsync(row) : Task.CompletedTask;
  protected Task DetailsAsync(PersonalListItemDto row) => Access?.Has(Permissions.Personals.ReadDetails) == true
    ? OpenDialog<Details>(row.Id) : Task.CompletedTask;
  protected Task EditAsync(PersonalListItemDto row) => CanWrite(row) && Access?.Has(Permissions.Personals.ReadDetails) == true && row.Status is Status.Inserted or Status.Modified
    ? OpenDialog<Edit>(row.Id) : Task.CompletedTask;
  protected Task AddAsync() => Access?.Has(Permissions.Personals.Write) == true ? OpenDialog<Add>() : Task.CompletedTask;

  protected async Task IpsAsync(PersonalListItemDto row)
  {
    if (disposed || operationPending || Access?.Has(Permissions.IpAddresses.Manage) != true ||
        row.Status is not (Status.Inserted or Status.Modified)) return;
    var result = await Dialogs.OpenAsync<IpAssignments>("",
      new Dictionary<string, object?> { ["PersonalId"] = row.Id },
      new DialogOptions { Width = "650px", ShowTitle = false, ContentCssClass = "rz-p-1" });
    await HandleResultAsync(result);
  }

  protected async Task ChangeStatusAsync(PersonalListItemDto row, StatusOperation operation)
  {
    if (disposed || operationPending || !CanWrite(row) ||
        !EntityStatusTransitions.CanApply(row.Status, operation, Access?.IsRoot == true)) return;
    var action = operation switch
    {
      StatusOperation.Archive => "Поместить выбранную запись в архив?",
      StatusOperation.Restore => "Восстановить выбранную запись?",
      StatusOperation.Block => "Заблокировать выбранную запись?",
      StatusOperation.Unblock => "Разблокировать выбранную запись?",
      StatusOperation.Delete => "Удалить выбранную запись?",
      _ => throw new ArgumentException("Неизвестная операция.")
    };
    operationPending = true;
    try
    {
      if (await Dialogs.Confirm(action, "Изменение статуса",
        new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" }) != true || disposed) return;
      switch (operation)
      {
        case StatusOperation.Archive: await Service.ArchiveAsync(row.Id, cts.Token, row.Version); break;
        case StatusOperation.Restore: await Service.RestoreAsync(row.Id, cts.Token, row.Version); break;
        case StatusOperation.Block: await Service.BlockAsync(row.Id, cts.Token, row.Version); break;
        case StatusOperation.Unblock: await Service.UnblockAsync(row.Id, cts.Token, row.Version); break;
        case StatusOperation.Delete: await Service.SoftDeleteAsync(row.Id, cts.Token, row.Version); break;
      }
      Notify(NotificationSeverity.Success, "Статус записи изменён.");
      await ReloadAsync();
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
    catch (Exception ex) { ShowError(ex, "Изменение статуса сотрудника"); }
    finally { operationPending = false; }
  }

  protected async Task ExportAsync()
  {
    if (disposed || operationPending || Access?.Has(Permissions.Personals.Export) != true) return;
    operationPending = true;
    try
    {
      var bytes = await Service.ExportAsync(Request(), cts.Token);
      if (disposed) return;
      await using var module = await JS.InvokeAsync<IJSObjectReference>("import", cts.Token, "./js/download.js");
      using var stream = new MemoryStream(bytes);
      using var reference = new DotNetStreamReference(stream);
      await module.InvokeVoidAsync("downloadFile", cts.Token, "Сотрудники.csv", reference);
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
    catch (Exception ex) { ShowError(ex, "Экспорт сотрудников"); }
    finally { operationPending = false; }
  }

  private void ShowError(Exception ex, string operation)
  {
    if (disposed) return;
    Logger.LogWarning(ex, "{Operation}", operation);
    error = UserOperationErrors.Message(ex);
    Notify(NotificationSeverity.Error, error);
  }

  private void Notify(NotificationSeverity severity, string detail)
  {
    if (disposed) return;
    Notifications.Notify(new NotificationMessage
    {
      Severity = severity, Summary = "Сотрудники", Detail = detail,
      Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;"
    });
  }

  public void Dispose()
  {
    if (disposed) return;
    disposed = true;
    loading?.Cancel();
    cts.Cancel();
    cts.Dispose();
  }
}
