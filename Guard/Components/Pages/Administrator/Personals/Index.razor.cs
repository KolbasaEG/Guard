using Guard.Components.Library;
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
  [Inject] public IPersonalService Service { get; set; } = default!;
  [Inject] public DialogService Dialogs { get; set; } = default!;
  [Inject] public IJSRuntime JS { get; set; } = default!;
  [Inject] public ILogger<Index> Logger { get; set; } = default!;
  [CascadingParameter] public UserAccessSnapshot? Access { get; set; }
  [CascadingParameter] public UserContext? UserContext { get; set; }
  private readonly CancellationTokenSource cts = new();
  protected RadzenDataGrid<PersonalListItemDto> grid = default!;
  protected IEnumerable<Personal> data = [];
  protected RadzenDataFilter<Personal> dataFilter = default!;
  private PersonalFilter? appliedFilter;
  private CancellationTokenSource? loading;
  private bool disposed, reloadPending = true;
  private UserAccessSnapshot? loadedAccess;
  private UserContext? loadedContext;
  [Inject] public IPermissionService PermissionService { get; set; } = default!;
  [Inject] public NotificationService Notifications { get; set; } = default!;
  protected SubdivisionHierarchyMode hierarchy = SubdivisionHierarchyMode.CurrentOnly;
  protected override void OnParametersSet() { if (!ReferenceEquals(loadedAccess, Access) || !ReferenceEquals(loadedContext, UserContext)) { loadedAccess = Access; loadedContext = UserContext; loading?.Cancel(); items = []; count = 0; reloadPending = true; } }
  protected override async Task OnAfterRenderAsync(bool firstRender) { if (disposed || !reloadPending) return; reloadPending = false; await grid.Reload(); if (!disposed) StateHasChanged(); }
  protected IReadOnlyList<PersonalListItemDto> items = [];
  protected int count;
  protected bool busy;
  protected string? search, error, sort;
  protected DataViewMode mode = DataViewMode.Active;
  private PersonalSearchRequest Request(int skip = 0, int take = 20) => new(Mode: mode, Skip:skip, Take:take, OrderBy:sort, Filter: appliedFilter, OwnSubdivision: true, IncludeChildren: hierarchy == SubdivisionHierarchyMode.IncludeChildren);
  protected bool CanWrite(PersonalListItemDto row) => Access?.Has(Permissions.Personals.Write) == true;
  protected async Task LoadData(LoadDataArgs args)
  {
    if (disposed) return;
    loading?.Cancel(); var request = CancellationTokenSource.CreateLinkedTokenSource(cts.Token); loading = request;
    var ct = request.Token;
    busy = true; error = null; sort = args.OrderBy;
    try {
      var result = await Service.SearchAsync(Request(args.Skip ?? 0, args.Top ?? 20), ct);
      ct.ThrowIfCancellationRequested(); await PermissionService.RequireAsync(PermissionsCode, ct);
      if (!disposed && ReferenceEquals(loading, request)) { items = result.Items; count = result.Count; }
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    catch (Exception ex) { if (!disposed && ReferenceEquals(loading, request)) { items = []; count = 0; Logger.LogWarning(ex, "Загрузка списка сотрудников"); error = "Не удалось загрузить список. Проверьте доступ."; } }
    finally { if (ReferenceEquals(loading, request)) { busy = false; loading = null; } request.Dispose(); }
  }
  private const string PermissionsCode = Guard.Core.Identity.Permissions.Personals.Read;
  protected async Task ResetPageAsync() { if (grid.CurrentPage == 0) await grid.Reload(); else await grid.FirstPage(true); }
  protected async Task ToggleHierarchyAsync() { hierarchy = hierarchy == SubdivisionHierarchyMode.CurrentOnly ? SubdivisionHierarchyMode.IncludeChildren : SubdivisionHierarchyMode.CurrentOnly; await ResetPageAsync(); }
  protected async Task ApplyFilter()
  {
    try { appliedFilter = new(All: dataFilter.LogicalFilterOperator == LogicalFilterOperator.And, Children: (dataFilter.Filters ?? []).Select(CloneFilter).ToArray()); await ResetPageAsync(); }
    catch (ArgumentException ex) { error = ex.Message; }
  }
  private static PersonalFilter CloneFilter(CompositeFilterDescriptor filter) => new(filter.Property, filter.FilterOperator.ToString(),
    filter.FilterValue as string, filter.LogicalFilterOperator == LogicalFilterOperator.And, filter.Filters?.Select(CloneFilter).ToArray());
  protected Task ReloadAsync() => grid.Reload();
  private async Task OpenDialog<T>(Guid? id = null) where T : IComponent
  {
    var args = id.HasValue ? new Dictionary<string,object?> { ["Id"] = id.Value } : null;
    var result = await Dialogs.OpenAsync<T>("", args, new DialogOptions { Width="850px", ShowTitle=false, ContentCssClass="rz-p-1" });
    if (Guard.Components.Library.Dialogs.EntityDialogResult.IsSuccess(result)) { NotifySuccess("Изменения сохранены."); await ReloadAsync(); }
  }
  protected Task OpenRow(PersonalListItemDto row) => Access?.Has(Permissions.Personals.ReadDetails) == true ? DetailsAsync(row) : Task.CompletedTask;
  protected Task DetailsAsync(PersonalListItemDto row) => OpenDialog<Details>(row.Id);
  protected Task EditAsync(PersonalListItemDto row) => OpenDialog<Edit>(row.Id);
  protected Task AddAsync() => OpenDialog<Add>();
  protected Task IpsAsync(PersonalListItemDto row) => Dialogs.OpenAsync<IpAssignments>("",
    new Dictionary<string,object?> { ["PersonalId"]=row.Id }, new DialogOptions { Width="650px", ShowTitle=false, ContentCssClass="rz-p-1" });
  protected async Task ChangeStatusAsync(PersonalListItemDto row, StatusOperation operation)
  {
    var action = operation switch {
      StatusOperation.Archive => "Поместить выбранную запись в архив?",
      StatusOperation.Restore => "Восстановить выбранную запись?",
      StatusOperation.Block => "Заблокировать выбранную запись?",
      StatusOperation.Unblock => "Разблокировать выбранную запись?",
      StatusOperation.Delete => "Удалить выбранную запись?",
      _ => throw new ArgumentException("Неизвестная операция.")
    };
    if (await Dialogs.Confirm(action, "Изменение статуса",
      new ConfirmOptions { OkButtonText="Да", CancelButtonText="Отмена" }) != true) return;
    try {
      switch (operation) {
        case StatusOperation.Archive: await Service.ArchiveAsync(row.Id, cts.Token, row.Version); break;
        case StatusOperation.Restore: await Service.RestoreAsync(row.Id, cts.Token, row.Version); break;
        case StatusOperation.Block: await Service.BlockAsync(row.Id, cts.Token, row.Version); break;
        case StatusOperation.Unblock: await Service.UnblockAsync(row.Id, cts.Token, row.Version); break;
        case StatusOperation.Delete: await Service.SoftDeleteAsync(row.Id, cts.Token, row.Version); break;
      }
      NotifySuccess("Статус записи изменён."); await ReloadAsync();
    }
    catch (Exception ex) { Logger.LogWarning(ex, "Изменение статуса сотрудника"); error = UserOperationErrors.Message(ex); }
  }
  protected async Task ExportAsync()
  {
    try {
      var bytes = await Service.ExportAsync(Request(), cts.Token);
      await using var module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/download.js");
      using var stream = new MemoryStream(bytes);
      using var reference = new DotNetStreamReference(stream);
      await module.InvokeVoidAsync("downloadFile", "Сотрудники.csv", reference);
    }
    catch (Exception ex) { Logger.LogWarning(ex, "Экспорт сотрудников"); error = "Экспорт недоступен."; }
  }
  private void NotifySuccess(string detail) => Notifications.Notify(new NotificationMessage { Severity=NotificationSeverity.Success, Summary="Сотрудник", Detail=detail, Style="position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" });
  public void Dispose() { disposed = true; loading?.Cancel(); cts.Cancel(); cts.Dispose(); }
}
