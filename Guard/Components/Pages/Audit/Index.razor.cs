using Guard.Components.Library;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Guard.Core.Services.Exports;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace Guard.Components.Pages.Audit;

public partial class Index : IAsyncDisposable
{
  [Parameter] public bool Diagnostic { get; set; }
  [Inject] protected IAuditSearchService Journal { get; set; } = default!;
  [Inject] protected IPermissionService Permissions { get; set; } = default!;
  [Inject] protected BrowserTimeService Time { get; set; } = default!;
  [Inject] protected ILogger<Index> Logger { get; set; } = default!;
  [Inject] protected NotificationService Notifications { get; set; } = default!;
  [Inject] protected DialogService Dialogs { get; set; } = default!;
  [CascadingParameter] public UserAccessSnapshot? Access { get; set; }
  private UserAccessSnapshot? loadedAccess;
  protected RadzenDataGrid<AuditEntryDto> grid = default!;
  protected RadzenDataFilter<AuditFilterFields> dataFilter = default!;
  protected IEnumerable<AuditFilterFields> filterData = [];
  protected IEnumerable<AuditEntryDto> items = [];
  protected AuditQueueHealthDto? health;
  protected int count;
  protected bool isLoading;
  protected string deliveryState = "all";
  private ExportFilter? appliedFilter;
  private readonly CancellationTokenSource lifetime = new();
  private CancellationTokenSource? loading;
  private bool reloadPending = true;
  private bool disposed;

  protected override void OnParametersSet()
  {
    if (ReferenceEquals(Access, loadedAccess)) return;
    loadedAccess = Access; loading?.Cancel(); items = []; count = 0; health = null; reloadPending = true;
  }
  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (disposed || !reloadPending || !Diagnostic && Access == null) return;
    reloadPending = false;
    try { await Time.InitializeAsync(); if (!disposed) await grid.Reload(); }
    catch (Exception error) { if (!disposed) Failed(error); }
  }
  protected async Task LoadData(LoadDataArgs args)
  {
    if (disposed || Time.Zone == null) return;
    loading?.Cancel();
    var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); loading = request; isLoading = true;
    try {
      var result = await Journal.SearchAsync(new(args.Skip ?? 0, args.Top ?? 20, args.OrderBy, deliveryState, appliedFilter), request.Token);
      var status = await Journal.GetHealthAsync(request.Token);
      if (!Diagnostic) await Permissions.RequireAsync(Core.Identity.Permissions.Audit.Read, request.Token);
      if (!disposed && ReferenceEquals(loading, request) && !request.IsCancellationRequested) {
        items = result.Items; count = result.Count; health = status;
      }
    }
    catch (OperationCanceledException) when (request.IsCancellationRequested) { }
    catch (Exception error) { if (!disposed && ReferenceEquals(loading, request)) { items = []; count = 0; health = null; Failed(error); } }
    finally { if (ReferenceEquals(loading, request)) { loading = null; isLoading = false; } request.Dispose(); }
  }
  protected async Task ApplyFilter()
  {
    try {
      appliedFilter = AdministrationListQuery.Capture(dataFilter, Time, AuditSearchFilter.Fields, AuditSearchFilter.Validate);
      await ResetPage();
    }
    catch (Exception error) { Failed(error); }
  }
  protected async Task ClearFilter() { await dataFilter.ClearFilters(); appliedFilter = null; await ResetPage(); }
  protected async Task ResetPage() { if (grid.CurrentPage == 0) await grid.Reload(); else await grid.FirstPage(true); }
  protected async Task ViewRow(AuditEntryDto entry) => await Dialogs.OpenAsync<Details>("",
    new Dictionary<string, object?> { [nameof(Details.EventId)] = entry.EventId },
    new DialogOptions { Width = "1000px", ShowTitle = false, ContentCssClass = "guard-entity-dialog-content" });
  private void Failed(Exception error)
  {
    Logger.LogError(error, "Загрузка журнала аудита");
    Notifications.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Аудит",
      Detail = "Не удалось загрузить журнал. Проверьте доступ и повторите попытку.",
      Style = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;" });
  }
  protected static string DeliveryText(AuditEntryDto entry) => !entry.DeliveryRequired ? "Отправка не требуется" : entry.SentAtUtc != null ? "Отправлено по TLS"
    : entry.LastError != null ? "Повторная попытка" : "Ожидает отправки";
  internal static string OutcomeText(string value) => value switch {
    "success" => "Успех", "failure" => "Ошибка", "denied" => "Запрещено", "started" => "Начато",
    "cancelled" => "Отменено", "unknown" => "Исход неизвестен", _ => value
  };
  public ValueTask DisposeAsync()
  {
    disposed = true; lifetime.Cancel(); loading?.Cancel(); lifetime.Dispose(); return ValueTask.CompletedTask;
  }
}
