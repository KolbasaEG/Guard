using System.Net;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen;

internal static class SynchronizationUiTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    await using var services = new ServiceCollection().AddLogging()
      .AddSingleton<ISynchronizationService, ServiceStub>().AddSingleton<NotificationService>()
      .AddSingleton<IApiDiagnosticService, DiagnosticStub>()
      .AddSingleton<IJSRuntime, NoJs>().BuildServiceProvider();
    await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
    await renderer.Dispatcher.InvokeAsync(async () =>
    {
      var root = await renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(Host.Root)] = true }));
      var html = WebUtility.HtmlDecode(root.ToHtmlString());
      check(html.Contains("Актуально по состоянию на") && html.Contains("Управление из API") && html.Contains("успешно"),
        "synchronization UI renders mode, actual date and localized result");
      check(html.Contains("Настройки") || html.Contains("Режим управления"), "Root sees synchronization settings");
      check(html.Contains("Источник данных") && html.Contains("Размер страницы запроса"), "Root sees API source and pagination settings");
      check(html.Contains("Логин Basic auth API") && html.Contains("Новый пароль Basic auth API"), "Root sees Basic authentication fields");
      check(html.Contains("Проверить подключение") && html.Contains("Полная сверка"), "Root UI exposes ping and distinct synchronization checkpoints");
      check(html.Contains("Изменения с даты"), "Root sees optional manual UpdatedAt date field");
      check(html.Contains("Запустить диагностику") && html.Contains("Обязательная дата UpdatedAt"), "Root sees separate diagnostic control with required date");
      check(html.Contains("02:00") && html.Contains("Часовой пояс сервера") && html.Contains("Последние 20 запусков", StringComparison.OrdinalIgnoreCase),
        "settings UI shows server time and run history");
      check(html.Contains("Внешний ID головного подразделения API"), "Root can set API head subdivision");
      var admin = await renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(Host.Root)] = false }));
      var adminHtml = WebUtility.HtmlDecode(admin.ToHtmlString());
      check(!adminHtml.Contains("Синхронизировать") && !adminHtml.Contains("Сохранить настройки") && !adminHtml.Contains("Последние 20 запусков"), "administrator cannot access Root synchronization page even with legacy permission");
      check(!adminHtml.Contains("Источник данных") && !adminHtml.Contains("Размер страницы запроса"), "API connection settings are only shown to Root");
      check(!adminHtml.Contains("Логин Basic auth API") && !adminHtml.Contains("Новый пароль Basic auth API"), "administrator cannot see Basic credential controls");
      check(!adminHtml.Contains("Изменения с даты"), "pool operator cannot choose manual date filter");
      check(!adminHtml.Contains("Запустить диагностику"), "diagnostic controls are hidden from pool operator");
      check(!adminHtml.Contains("Внешний ID головного подразделения API"), "API head setting is hidden from operator");
      var actuality = await renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(Host.Root)] = false, [nameof(Host.Actuality)] = true }));
      var actualityHtml = WebUtility.HtmlDecode(actuality.ToHtmlString());
      check(actualityHtml.Contains("Актуально по состоянию на") && actualityHtml.Contains("Синхронизация отключена"), "ordinary user sees previous global date in local mode");
      check(!actualityHtml.Contains("Синхронизировать") && !actualityHtml.Contains("Изменения с даты") && !actualityHtml.Contains("Последняя попытка"), "actuality panel contains no controls or attempt details");
      var actualityStub = (ServiceStub)services.GetRequiredService<ISynchronizationService>();
      actualityStub.EmptyActuality = true; actualityStub.ActualityMode = DataManagementMode.Api;
      var emptyActuality = await renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(Host.Root)] = false, [nameof(Host.Actuality)] = true }));
      var emptyHtml = WebUtility.HtmlDecode(emptyActuality.ToHtmlString());
      check(emptyHtml.Contains("Данные из API ещё не актуализированы") && !emptyHtml.Contains("Синхронизация отключена"), "API mode with no successful run shows explicit unknown freshness");
      actualityStub.EmptyActuality = false;
      ActualityProbe? actualityProbe = null;
      var reloads = 0;
      var rejectReload = false;
      var recovery = await renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(new Dictionary<string, object?> {
        [nameof(Host.Actuality)] = true,
        [nameof(Host.CaptureActuality)] = (Action<ActualityProbe>)(value => actualityProbe = value),
        [nameof(Host.ReloadTable)] = (Action)(() => { reloads++; if (rejectReload) throw new InvalidOperationException("Test table failure"); }) }));
      check(reloads == 0, "initial freshness load does not duplicate table loading");
      actualityStub.RejectActuality = true;
      await actualityProbe!.RefreshForTestAsync();
      check(WebUtility.HtmlDecode(recovery.ToHtmlString()).Contains("Не удалось проверить актуальность"), "temporary freshness failure is displayed");
      actualityStub.RejectActuality = false;
      await actualityProbe.RefreshForTestAsync();
      check(reloads == 0, "recovery with unchanged date avoids redundant table reload");
      actualityStub.RejectActuality = true;
      await actualityProbe.RefreshForTestAsync();
      actualityStub.ActualityAt = actualityStub.ActualityAt!.Value.AddMinutes(1);
      actualityStub.RejectActuality = false;
      await actualityProbe.RefreshForTestAsync();
      check(reloads == 1, "new global date after temporary failure reloads table");
      await actualityProbe.RefreshForTestAsync();
      check(reloads == 1, "successfully applied date is not reloaded twice");
      actualityStub.ActualityAt = actualityStub.ActualityAt!.Value.AddMinutes(1);
      rejectReload = true;
      await actualityProbe.RefreshForTestAsync();
      check(reloads == 2 && WebUtility.HtmlDecode(recovery.ToHtmlString()).Contains("Не удалось проверить актуальность"), "failed table callback does not acknowledge new date");
      rejectReload = false;
      await actualityProbe.RefreshForTestAsync();
      check(reloads == 3 && !WebUtility.HtmlDecode(recovery.ToHtmlString()).Contains("Не удалось проверить актуальность"), "next freshness check retries failed table reload and clears error");
      await actualityProbe.RefreshForTestAsync();
      check(reloads == 3, "successful retry acknowledges date and stops further reloads");
      var confirmed = false;
      var confirmations = 0;
      ActualityProbe? confirmationProbe = null;
      var confirmationView = await renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(new Dictionary<string, object?> {
        [nameof(Host.Actuality)] = true,
        [nameof(Host.CaptureActuality)] = (Action<ActualityProbe>)(value => confirmationProbe = value),
        [nameof(Host.ReloadWithResult)] = (Func<Task<bool>>)(() => { confirmations++; return Task.FromResult(confirmed); }) }));
      actualityStub.ActualityAt = actualityStub.ActualityAt!.Value.AddMinutes(1);
      await confirmationProbe!.RefreshForTestAsync();
      check(confirmations == 1 && WebUtility.HtmlDecode(confirmationView.ToHtmlString()).Contains("Загрузка будет повторена автоматически"), "handled LoadData failure without exception does not acknowledge new date");
      confirmed = true;
      await confirmationProbe.RefreshForTestAsync();
      check(confirmations == 2 && !WebUtility.HtmlDecode(confirmationView.ToHtmlString()).Contains("Загрузка будет повторена автоматически"), "explicit successful result acknowledges date after retry");
      await confirmationProbe.RefreshForTestAsync();
      check(confirmations == 2, "confirmed table date avoids redundant updates");
      PanelProbe? panel = null;
      var failure = await renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(new Dictionary<string, object?> {
        [nameof(Host.Root)] = true, [nameof(Host.CapturePanel)] = (Action<PanelProbe>)(value => panel = value) }));
      await panel!.ClickAsync();
      check(WebUtility.HtmlDecode(failure.ToHtmlString()).Contains("Диагностическая ошибка запуска"), "rejected synchronization displays service error in panel");
      await panel.RefreshForTestAsync();
      check(WebUtility.HtmlDecode(failure.ToHtmlString()).Contains("Диагностическая ошибка запуска"), "status refresh preserves synchronization start error");
      var stub = (ServiceStub)services.GetRequiredService<ISynchronizationService>();
      stub.RejectRun = false;
      await panel.ClickAsync();
      check(stub.LastEnqueued && WebUtility.HtmlDecode(failure.ToHtmlString()).Contains("ожидает запуска"), "manual UI queues request and displays pending status instead of waiting for HTTP download");
    });
  }

  public sealed class Host : ComponentBase
  {
    [Parameter] public bool Root { get; set; }
    [Parameter] public bool Actuality { get; set; }
    [Parameter] public Action<ActualityProbe>? CaptureActuality { get; set; }
    [Parameter] public Action? ReloadTable { get; set; }
    [Parameter] public Func<Task<bool>>? ReloadWithResult { get; set; }
    [Parameter] public Action<PanelProbe>? CapturePanel { get; set; }
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
      builder.OpenComponent<CascadingValue<UserAccessSnapshot>>(0);
      builder.AddAttribute(1, "Value", new UserAccessSnapshot("test", Root, [Permissions.Subdivisions.Read, Permissions.Synchronization.Run]));
      builder.AddAttribute(2, "ChildContent", (RenderFragment)(child => {
        if (Actuality)
        {
          child.OpenComponent<ActualityProbe>(0);
          child.AddAttribute(1, "ReadPermission", Permissions.Subdivisions.Read);
          if (ReloadWithResult != null) child.AddAttribute(2, "ReloadData", ReloadWithResult);
          else if (ReloadTable != null) child.AddAttribute(2, "ReloadData", (Func<Task<bool>>)(() => { ReloadTable(); return Task.FromResult(true); }));
          if (CaptureActuality != null) child.AddComponentReferenceCapture(3, component => CaptureActuality((ActualityProbe)component));
          child.CloseComponent();
        }
        else if (CapturePanel != null)
        {
          child.OpenComponent<PanelProbe>(0);
          child.AddComponentReferenceCapture(1, component => CapturePanel((PanelProbe)component));
          child.CloseComponent();
        }
        else { child.OpenComponent<Guard.Components.Pages.Workspaces.Administrator.Synchronization>(0); child.CloseComponent(); }
      }));
      builder.CloseComponent();
    }
  }
  public sealed class PanelProbe : Guard.Components.Pages.Administrator.Synchronization.StatusPanel
  {
    public async Task ClickAsync() { await RunAsync(); StateHasChanged(); }
    public async Task RefreshForTestAsync() { await RefreshAsync(); StateHasChanged(); }
  }
  public sealed class ActualityProbe : Guard.Components.Pages.Administrator.Synchronization.ActualityPanel
  {
    public async Task RefreshForTestAsync() { await RefreshAsync(); StateHasChanged(); }
  }
  private sealed class ServiceStub : ISynchronizationService
  {
    public bool EmptyActuality { get; set; }
    public DataManagementMode ActualityMode { get; set; } = DataManagementMode.Local;
    public bool RejectActuality { get; set; }
    public DateTime? ActualityAt { get; set; } = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
    public Task<DirectoryActuality> GetActualityAsync(string readPermission, CancellationToken ct = default) => RejectActuality
      ? throw new InvalidOperationException("Test freshness failure") : Task.FromResult(new DirectoryActuality(ActualityMode, EmptyActuality ? null : ActualityAt));
    public bool RejectRun { get; set; } = true;
    public bool LastEnqueued { get; private set; }
    public Task CheckConnectionAsync(CancellationToken ct = default) => throw new NotSupportedException();
    private readonly SynchronizationRun run = new() { StartedAt = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc), Status = SynchronizationStatus.Succeeded };
    public Task<SynchronizationOverview> GetOverviewAsync(bool classifiers = false, bool includeChildren = true, CancellationToken ct = default, bool allSubdivisions = false) =>
      Task.FromResult(new SynchronizationOverview(new() { Mode = DataManagementMode.Api }, null, run.StartedAt, run, true));
    public Task<IReadOnlyList<SynchronizationRun>> GetHistoryAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SynchronizationRun>>([run]);
    public Task SaveSettingsAsync(DataManagementMode mode, bool enabled, TimeOnly time, Guid version, CancellationToken ct = default, string? apiBaseUrl = null, int? apiPageSize = null, string? apiUsername = null, string? apiPassword = null, long? apiHeadSubdivisionId = null) => throw new NotSupportedException();
    public Task<SynchronizationRun> RunAsync(CancellationToken ct = default, DateOnly? updatedSince = null, bool enqueue = false)
    {
      if (RejectRun) throw new EntityRuleException("Диагностическая ошибка запуска.");
      LastEnqueued = enqueue; run.Status = SynchronizationStatus.Queued; return Task.FromResult(run);
    }
  }
  private sealed class DiagnosticStub : IApiDiagnosticService
  {
    public Task<ApiDiagnosticResult> RunAsync(DateOnly updatedSince, CancellationToken ct = default, IProgress<string>? progress = null)
      => throw new NotSupportedException("Rendering test does not initiate API requests.");
  }
  private sealed class NoJs : IJSRuntime
  {
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => ValueTask.FromResult(default(TValue)!);
  }
}
