using Guard.Components.Library;
using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace Guard.Components.Pages.ExportArchive;

public partial class Archive : IDisposable
{
  [Inject] protected IDataExportService Service { get; set; } = default!;
  [Inject] protected BrowserTimeService Time { get; set; } = default!;
  [Inject] protected ILogger<Archive> Logger { get; set; } = default!;
  [Inject] protected DialogService Dialogs { get; set; } = default!;
  [Inject] protected NotificationService Notifications { get; set; } = default!;
  protected IEnumerable<ExportJob> data = [];
  protected IEnumerable<ExportArchiveItem> filteredData = [];
  protected RadzenDataGrid<ExportArchiveItem> grid = default!;
  protected RadzenDataFilter<ExportJob> dataFilter = default!;
  protected int count; protected bool isLoading;
  private ExportFilter? applied; private CancellationTokenSource? loading; private bool disposed;
  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (firstRender) { try { await Time.InitializeAsync(); await grid.Reload(); if (!disposed) StateHasChanged(); } catch (Exception ex) { Error(ex); } }
  }
  protected async Task LoadAsync(LoadDataArgs args)
  {
    if (disposed || Time.Zone == null) return;
    loading?.Cancel(); var request = new CancellationTokenSource(); loading = request; isLoading = true;
    try {
      var result = await Service.SearchArchiveAsync(new(args.Skip ?? 0, args.Top ?? 20, args.OrderBy, applied), request.Token);
      request.Token.ThrowIfCancellationRequested();
      if (!disposed && ReferenceEquals(loading, request)) { filteredData = result.Items; count = result.Count; }
    } catch (OperationCanceledException) when (request.IsCancellationRequested) { }
    catch (Exception ex) { if (!disposed && ReferenceEquals(loading, request)) { filteredData = []; count = 0; Error(ex); } }
    finally { if (ReferenceEquals(loading, request)) { loading = null; isLoading = false; } request.Dispose(); }
  }
  protected async Task ApplyAsync()
  {
    try {
      var candidate = AdministrationListQuery.Capture(dataFilter, Time, ExportArchiveQuery.Fields, ExportArchiveQuery.ValidateFilter);
      applied = candidate; if (grid.CurrentPage == 0) await grid.Reload(); else await grid.FirstPage(true);
    } catch (Exception ex) { Error(ex); }
  }
  protected async Task ConfigureAsync()
  {
    var result = await Dialogs.OpenAsync<Retention>("", null, new DialogOptions { Width = "500px", ShowTitle = false, ContentCssClass = "rz-p-1" });
    if (result is true) Notifications.Notify(new NotificationMessage { Severity = NotificationSeverity.Success, Summary = "Хранение", Detail = "Новый срок применяется к новым выгрузкам.", Style = NotificationStyle });
  }
  protected Task ViewAsync(Guid id) => Dialogs.OpenAsync<Preview>("", new Dictionary<string, object?> { ["Id"] = id }, new DialogOptions { Width = "1200px", ShowTitle = false, ContentCssClass = "rz-p-1" });
  protected string FormatTime(DateTimeOffset value) => Time.Zone == null ? "…" : TimeZoneInfo.ConvertTime(value, Time.Zone).ToString("dd.MM.yyyy HH:mm");
  private const string NotificationStyle = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;";
  private void Error(Exception ex) { Guard.Core.Logging.OperationFailureLogging.LogOperationFailure(Logger, ex, "Архив выгрузок"); Notifications.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Архив выгрузок", Detail = UserOperationErrors.Message(ex), Style = NotificationStyle }); }
  public void Dispose() { disposed = true; loading?.Cancel(); }
}
