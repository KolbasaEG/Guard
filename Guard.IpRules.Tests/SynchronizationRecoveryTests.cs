using System.Net;
using System.Reflection;
using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Radzen;
using Page = Guard.Components.Pages.Synchronization.Page;
using Panel = Guard.Components.Pages.Synchronization.StatusPanel;

internal static class SynchronizationRecoveryTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var service = new Stub();
    using (var page = CreatePage(service))
    {
      await Call(page, "ReloadSettingsAsync");
      var form = Field<Page.SettingsForm>(page, "form");
      check(form.CompleteSnapshotGuaranteed, "settings form loads stored snapshot confirmation");
      form.CompleteSnapshotGuaranteed = false;
      var version = Field<SynchronizationSettings>(page, "settings").Version;
      form.ApiBaseUrl = "https://edited.example.test"; form.ApiPassword = "test-new-password";
      service.FailHistory = 1;
      await Call(page, "ReloadRunsAsync");
      check(Field<string?>(page, "runsError") != null && Field<string?>(page, "settingsError") == null,
        "history failure is independent of settings");
      check(ReferenceEquals(form, Field<Page.SettingsForm>(page, "form")) &&
        form.ApiPassword == "test-new-password" && Field<SynchronizationSettings>(page, "settings").Version == version,
        "history failure preserves edited fields, password and version");

      await using var panel = CreatePanel(service);
      Connect(panel, "LastAttemptChanged", () => Call(page, "ReloadRunsAsync"));
      Connect(panel, "ManualRefreshRequested", () => Call(page, "ReloadRunsAsync"));
      ConnectCycle(panel, page);
      await Call(panel, "RunPollingCycleAsync");
      check(Field<string?>(page, "runsError") == null && Field<IReadOnlyList<SynchronizationRun>>(page, "history").Count == 1,
        "successful poll retries failed history even when the attempt is unchanged");
      var reads = service.HistoryReads;
      await Call(panel, "RunPollingCycleAsync");
      check(service.HistoryReads == reads, "healthy unchanged poll does not reload history");

      service.FailHistory = 1;
      await Call(page, "ReloadRunsAsync");
      await Call(panel, "RefreshManuallyAsync");
      check(Field<string?>(page, "runsError") == null, "manual refresh recovers unchanged history");
      reads = service.HistoryReads;
      service.Status = SynchronizationStatus.Failed;
      await Call(panel, "RefreshManuallyAsync");
      check(service.HistoryReads == reads + 1, "manual refresh with changed attempt reads history only once");
      check(ReferenceEquals(form, Field<Page.SettingsForm>(page, "form")) &&
        form.ApiBaseUrl == "https://edited.example.test", "refresh and retries preserve unsaved form");
      check(Field<SynchronizationSettings>(page, "settings").Version == version,
        "refresh and retries preserve the version of unsaved settings");

      service.FailHistory = 1;
      await Call(page, "SaveAsync");
      check(Field<SynchronizationSettings>(page, "settings").Version == service.Version &&
        !Field<bool>(page, "settingsNeedReload") && Field<string?>(page, "runsError") != null,
        "successful save accepts new settings version despite history failure");
      check(Field<Page.SettingsForm>(page, "form").ApiPassword == "", "successful save clears new password");
      check(!service.CompleteSnapshotGuaranteed && !Field<Page.SettingsForm>(page, "form").CompleteSnapshotGuaranteed,
        "settings form submits and reloads revoked snapshot confirmation");

      service.FailReloadAfterSave = true;
      await Call(page, "SaveAsync");
      check(Field<bool>(page, "settingsNeedReload") &&
        Field<string>(page, "settingsError").Contains("Настройки сохранены"),
        "failed settings read after save is distinguished from failed save");
      var saves = service.SaveCalls;
      await Call(page, "SaveAsync");
      check(service.SaveCalls == saves, "stale settings version cannot be saved again");
      await Call(page, "RetrySettingsAsync");
      check(!Field<bool>(page, "settingsNeedReload") &&
        Field<SynchronizationSettings>(page, "settings").Version == service.Version,
        "explicit retry restores current version and enables saving");

      service.FailReloadAfterSave = false; service.RejectSave = true;
      form = Field<Page.SettingsForm>(page, "form");
      form.ApiPassword = "test-preserved-password";
      var messages = Field<NotificationService>(page, "Notifications", property: true).Messages.Count;
      version = Field<SynchronizationSettings>(page, "settings").Version;
      await Call(page, "SaveAsync");
      check(form.ApiPassword == "test-preserved-password" &&
        Field<SynchronizationSettings>(page, "settings").Version == version &&
        Field<NotificationService>(page, "Notifications", property: true).Messages.Count == messages &&
        Field<string?>(page, "error") != null,
        "rejected save preserves input and version and emits no success");
    }

    service = new Stub();
    await using (var panel = CreatePanel(service))
    {
      var changes = 0; var successes = 0;
      Connect(panel, "LastAttemptChanged", () => { changes++; return Task.CompletedTask; });
      Connect(panel, "Synchronized", () => { successes++; return Task.CompletedTask; });
      await panel.RefreshAsync();
      service.FailOverview = 1;
      await panel.RefreshAsync();
      service.Status = SynchronizationStatus.Succeeded;
      await panel.RefreshAsync();
      await panel.RefreshAsync();
      check(changes == 1 && successes == 1,
        "recovery after overview failure detects completion once and preserves Synchronized");
    }

    service = new Stub();
    using (var page = CreatePage(service))
    {
      var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      service.BeforeHistory = _ => gate.Task;
      var first = Call(page, "ReloadRunsAsync");
      service.Status = SynchronizationStatus.Succeeded;
      var second = Call(page, "ReloadRunsAsync");
      check(service.HistoryReads == 1, "new run update does not start a parallel history read");
      gate.SetResult();
      await Task.WhenAll(first, second);
      check(service.HistoryReads == 2 && Field<SynchronizationRun>(page, "last").Status == SynchronizationStatus.Succeeded,
        "update arriving during a read is followed by a fresh snapshot instead of leaving stale status");
    }

    service = new Stub();
    using (var page = CreatePage(service))
    {
      var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      service.BeforeHistory = _ => gate.Task;
      var first = Call(page, "ReloadRunsAsync");
      var second = Call(page, "ReloadRunsAsync");
      check(service.HistoryReads == 1, "overlapping reload requests share one history operation");
      var ct = service.LastHistoryToken;
      page.Dispose();
      gate.SetResult();
      await Task.WhenAll(first, second);
      check(ct.IsCancellationRequested && Field<IReadOnlyList<SynchronizationRun>>(page, "history").Count == 0,
        "leaving page cancels pending read and ignores its late result");
    }

    await CheckPollingRecoveryAsync(check);
    await CheckRenderingAsync(check);
  }

  private static async Task CheckPollingRecoveryAsync(Action<bool, string> check)
  {
    var stub = new Stub();
    using (var page = CreatePage(stub))
    {
      await Call(page, "ReloadSettingsAsync");
      var version = Field<SynchronizationSettings>(page, "settings").Version;
      var form = Field<Page.SettingsForm>(page, "form");
      form.ApiBaseUrl = "https://unsaved.example.test";
      stub.FailOverview = int.MaxValue; stub.FailHistory = 2;
      await Call(page, "ReloadRunsAsync");
      await using var panel = CreatePanel(stub);
      ConnectCycle(panel, page);
      await Call(panel, "RunPollingCycleAsync");
      check(stub.HistoryReads == 2 && Field<string?>(page, "runsError") != null,
        "failed overview cycle retries failing history exactly once");
      await Call(panel, "RunPollingCycleAsync");
      check(stub.HistoryReads == 3 && Field<string?>(page, "runsError") == null,
        "next failed overview cycle recovers history without manual action");
      await Call(panel, "RunPollingCycleAsync");
      check(stub.HistoryReads == 3, "failed overview cycles do not reread healthy history");
      check(ReferenceEquals(form, Field<Page.SettingsForm>(page, "form")) &&
        form.ApiBaseUrl == "https://unsaved.example.test" && Field<SynchronizationSettings>(page, "settings").Version == version,
        "recovery during overview outage preserves edited settings and version");

      stub.FailHistory = 1;
      await Call(page, "ReloadRunsAsync");
      var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      stub.BeforeHistory = _ => gate.Task;
      Connect(panel, "ManualRefreshRequested", () => Call(page, "ReloadRunsAsync"));
      var reads = stub.HistoryReads;
      var manual = Call(panel, "RefreshManuallyAsync");
      var cycle = Call(panel, "RunPollingCycleAsync");
      check(stub.HistoryReads == reads + 1, "manual and background refresh do not start parallel history reads");
      gate.SetResult();
      await Task.WhenAll(manual, cycle);
      check(Field<string?>(page, "runsError") == null && Field<bool>(page, "runsLoaded"),
        "overlapping manual and background recovery applies valid history");
    }

    stub = new Stub();
    using (var page = CreatePage(stub))
    {
      await Call(page, "ReloadRunsAsync");
      await using var panel = CreatePanel(stub);
      Connect(panel, "LastAttemptChanged", () => Call(page, "ReloadRunsAsync"));
      ConnectCycle(panel, page);
      await panel.RefreshAsync();
      stub.Status = SynchronizationStatus.Succeeded; stub.FailHistory = 1;
      var reads = stub.HistoryReads;
      await Call(panel, "RunPollingCycleAsync");
      check(stub.HistoryReads == reads + 1 && Field<string?>(page, "runsError") != null,
        "failed reload for changed attempt is not retried twice within one cycle");
      await Call(panel, "RunPollingCycleAsync");
      check(stub.HistoryReads == reads + 2 && Field<string?>(page, "runsError") == null,
        "failed attempt reload is retried in the following cycle");
      stub.Status = SynchronizationStatus.Running;
      reads = stub.HistoryReads;
      await Call(panel, "RunPollingCycleAsync");
      check(stub.HistoryReads == reads + 1, "successful changed-attempt reload is not duplicated at cycle end");
    }

    stub = new Stub();
    using (var page = CreatePage(stub))
    {
      stub.FailHistory = 1;
      await Call(page, "ReloadRunsAsync");
      var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
      stub.BeforeOverview = _ => gate.Task;
      var panel = CreatePanel(stub);
      ConnectCycle(panel, page);
      var cycle = Call(panel, "RunPollingCycleAsync");
      await panel.DisposeAsync();
      gate.SetResult();
      await cycle;
      check(stub.HistoryReads == 1, "closing panel before overview completes prevents history retry");
    }
  }

  private static async Task CheckRenderingAsync(Action<bool, string> check)
  {
    var stub = new Stub { FailHistory = 10 };
    await using var services = new ServiceCollection().AddLogging()
      .AddSingleton<ISynchronizationService>(stub).AddSingleton<NotificationService>()
      .AddSingleton<IApiDiagnosticService, DiagnosticStub>()
      .AddSingleton<IJSRuntime, NoJs>().BuildServiceProvider();
    await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
    await renderer.Dispatcher.InvokeAsync(async () =>
    {
      var root = await renderer.RenderComponentAsync<Host>();
      var html = WebUtility.HtmlDecode(root.ToHtmlString());
      check(html.Contains("Сохранить настройки") && html.Contains("Повторить загрузку запусков") &&
        !html.Contains("Запусков пока нет."),
        "rendered history failure retains settings and does not pretend history is empty");
      check(stub.HistoryReads <= 2, "refresh callback does not cause a render/request loop");
    });
    stub.FailHistory = 0; stub.FailOverview = int.MaxValue;
    await renderer.Dispatcher.InvokeAsync(async () =>
    {
      var root = await renderer.RenderComponentAsync<Host>();
      var html = WebUtility.HtmlDecode(root.ToHtmlString());
      check(html.Contains("Повторить загрузку настроек") && html.Contains("Полная выгрузка") &&
        !html.Contains("Сохранить настройки"),
        "persistent settings failure still renders independent history and settings retry");
      check(stub.HistoryReads > 0, "history is queried while overview remains unavailable");
    });

    stub.FailOverview = 0;
    var overviewGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    stub.BeforeOverview = _ => overviewGate.Task;
    await renderer.Dispatcher.InvokeAsync(async () =>
    {
      var root = renderer.BeginRenderingComponent<Host>();
      var html = WebUtility.HtmlDecode(root.ToHtmlString());
      check(html.Contains("Загрузка запусков") && !html.Contains("Запусков пока нет."),
        "slow initial settings load does not display a false empty history");
      overviewGate.SetResult();
      await root.QuiescenceTask;
    });
    stub.BeforeOverview = null;

    var historyGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    stub.BeforeHistory = _ => historyGate.Task;
    await renderer.Dispatcher.InvokeAsync(async () =>
    {
      var root = renderer.BeginRenderingComponent<Host>();
      var html = WebUtility.HtmlDecode(root.ToHtmlString());
      check(html.Contains("Загрузка запусков") && !html.Contains("Запусков пока нет."),
        "pending initial history read displays loading instead of empty results");
      historyGate.SetResult();
      await root.QuiescenceTask;
      check(WebUtility.HtmlDecode(root.ToHtmlString()).Contains("Полная выгрузка"),
        "completed first history read displays received runs");
    });
    stub.BeforeHistory = null;

    stub.HistoryItems = [];
    await renderer.Dispatcher.InvokeAsync(async () =>
    {
      var root = await renderer.RenderComponentAsync<Host>();
      var html = WebUtility.HtmlDecode(root.ToHtmlString());
      check(html.Contains("Запусков пока нет.") && !html.Contains("Загрузка запусков"),
        "only a successful empty history response displays the empty state");
    });

    stub.HistoryItems = [new() { Id = Guid.NewGuid(), Status = SynchronizationStatus.Failed },
      new() { Id = Guid.NewGuid(), Status = SynchronizationStatus.Succeeded }];
    using (var page = CreatePage(stub))
    {
      await Call(page, "ReloadRunsAsync");
      check(ReferenceEquals(Field<SynchronizationRun>(page, "last"), stub.HistoryItems[0]),
        "latest attempt is taken from the first history entry, not a separate overview");
    }

    stub.HistoryItems = null; stub.FailHistory = 10;
    await renderer.Dispatcher.InvokeAsync(async () =>
    {
      Page? page = null;
      var root = await renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(new Dictionary<string, object?> {
        [nameof(Host.Capture)] = (Action<Page>)(value => page = value) }));
      check(WebUtility.HtmlDecode(root.ToHtmlString()).Contains("Повторить загрузку запусков") &&
        !WebUtility.HtmlDecode(root.ToHtmlString()).Contains("Запусков пока нет."),
        "failed first history read is an error, not an empty history");
      stub.FailHistory = 0; stub.FailOverview = int.MaxValue;
      var panel = Field<Panel>(page!, "statusPanel");
      await Call(panel, "RunPollingCycleAsync");
      // PollAsync renders the panel after each cycle, including a failed overview.
      typeof(ComponentBase).GetMethod("StateHasChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, null);
      var html = WebUtility.HtmlDecode(root.ToHtmlString());
      check(html.Contains("Полная выгрузка") && !html.Contains("Повторить загрузку запусков") &&
        html.Contains("Не удалось проверить актуальность данных"),
        "failed overview cycle restores rendered history while retaining overview warning");
    });
  }

  private static Page CreatePage(Stub service)
  {
    var page = new Page();
    Inject(page, "Service", service); Inject(page, "Logger", NullLogger<Page>.Instance);
    Inject(page, "Notifications", new NotificationService());
    Inject(page, "Access", new UserAccessSnapshot("test-root", true, []));
    return page;
  }
  private static Panel CreatePanel(Stub service)
  {
    var panel = new Panel();
    Inject(panel, "Service", service); Inject(panel, "Logger", NullLogger<Panel>.Instance);
    Inject(panel, "Access", new UserAccessSnapshot("test-root", true, []));
    return panel;
  }
  private static void Inject(object target, string name, object value) =>
    target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);
  private static T Field<T>(object target, string name, bool property = false) => (T)(property
    ? target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!
    : target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!);
  private static Task Call(object target, string name, params object?[] args) =>
    (Task)target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args)!;
  private static void Connect(Panel panel, string name, Func<Task> callback) =>
    Inject(panel, name, EventCallback.Factory.Create(new object(), callback));
  private static void ConnectCycle(Panel panel, Page page) =>
    Inject(panel, "PollingCycleCompleted", EventCallback.Factory.Create<bool>(new object(),
      value => Call(page, "OnPollingCycleCompletedAsync", value)));

  private sealed class Stub : ISynchronizationService
  {
    public Guid Version = Guid.NewGuid();
    public int FailOverview, FailHistory, HistoryReads, SaveCalls;
    public bool FailReloadAfterSave, RejectSave;
    public bool CompleteSnapshotGuaranteed = true;
    public SynchronizationStatus Status = SynchronizationStatus.Running;
    public Func<CancellationToken, Task>? BeforeHistory;
    public Func<CancellationToken, Task>? BeforeOverview;
    public IReadOnlyList<SynchronizationRun>? HistoryItems;
    public CancellationToken LastHistoryToken;
    private readonly Guid runId = Guid.NewGuid();
    private string apiUrl = "https://saved.example.test";
    private SynchronizationRun Run => new() {
      Id = runId, Status = Status, Phase = "Fetch", StartedAt = new(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc) };
    public async Task<SynchronizationOverview> GetOverviewAsync(bool classifiers = false, bool includeChildren = true, CancellationToken ct = default, bool allSubdivisions = false)
    {
      ct.ThrowIfCancellationRequested();
      if (FailOverview > 0) { FailOverview--; throw new IOException("Test overview failure"); }
      if (BeforeOverview != null) await BeforeOverview(ct);
      return new SynchronizationOverview(new() { Version = Version, ApiBaseUrl = apiUrl, CompleteSnapshotGuaranteed = CompleteSnapshotGuaranteed }, null, null, Run, false);
    }
    public async Task<IReadOnlyList<SynchronizationRun>> GetHistoryAsync(CancellationToken ct = default)
    {
      HistoryReads++; LastHistoryToken = ct;
      if (FailHistory > 0) { FailHistory--; throw new IOException("Test history failure"); }
      if (BeforeHistory != null) await BeforeHistory(ct);
      return HistoryItems ?? [Run];
    }
    public Task SaveSettingsAsync(DataManagementMode mode, bool enabled, TimeOnly time, Guid version, CancellationToken ct = default, string? apiBaseUrl = null, int? apiPageSize = null, string? apiUsername = null, string? apiPassword = null, long? apiHeadSubdivisionId = null, bool? completeSnapshotGuaranteed = null)
    {
      SaveCalls++;
      if (RejectSave) throw new EntityRuleException("Сохранение отклонено.");
      if (version != Version) throw new EntityRuleException("Версия устарела.");
      Version = Guid.NewGuid(); apiUrl = apiBaseUrl ?? apiUrl;
      CompleteSnapshotGuaranteed = completeSnapshotGuaranteed ?? CompleteSnapshotGuaranteed;
      if (FailReloadAfterSave) FailOverview = 1;
      return Task.CompletedTask;
    }
    public Task<DirectoryActuality> GetActualityAsync(string readPermission, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<SynchronizationRun> RunAsync(CancellationToken ct = default, DateOnly? updatedSince = null, bool enqueue = false) => throw new NotSupportedException();
    public Task CheckConnectionAsync(CancellationToken ct = default) => throw new NotSupportedException();
  }
  public sealed class Host : ComponentBase
  {
    [Parameter] public Action<Page>? Capture { get; set; }
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
      builder.OpenComponent<CascadingValue<UserAccessSnapshot>>(0);
      builder.AddAttribute(1, "Value", new UserAccessSnapshot("test-root", true, []));
      builder.AddAttribute(2, "ChildContent", (RenderFragment)(child => {
        child.OpenComponent<Page>(0);
        child.AddComponentReferenceCapture(1, value => Capture?.Invoke((Page)value));
        child.CloseComponent();
      }));
      builder.CloseComponent();
    }
  }
  private sealed class DiagnosticStub : IApiDiagnosticService
  {
    public Task<ApiDiagnosticResult> RunAsync(DateOnly updatedSince, CancellationToken ct = default, IProgress<string>? progress = null) => throw new NotSupportedException();
  }
  private sealed class NoJs : IJSRuntime
  {
    public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!);
    public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args) => ValueTask.FromResult(default(T)!);
  }
}
