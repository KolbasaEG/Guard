using System.Reflection;
using Guard.Components.Pages.Synchronization;
using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Radzen;

internal static class SynchronizationFeedbackTests
{
  private const string NotificationStyle = "position: fixed; top: 3%; left: 50%; transform: translate(-50%, -50%); z-index: 1000;";

  public static async Task RunAsync(Action<bool, string> check)
  {
    var notifications = new NotificationService();
    var service = new SyncStub();
    using (var page = CreatePage(service, notifications))
    {
      await Call(page, "CheckConnectionAsync");
      check(notifications.Messages.Count == 1 && notifications.Messages[0].Severity == NotificationSeverity.Success &&
        notifications.Messages[0].Detail.Contains("подтверждены") && notifications.Messages[0].Style == NotificationStyle,
        "connection probe success emits exactly one success notification at the standard position");
      notifications.Messages.Clear();
      service.Probe = _ => throw new IOException("private-host: secret diagnostic detail");
      await Call(page, "CheckConnectionAsync");
      check(notifications.Messages.Count == 1 && notifications.Messages[0].Severity == NotificationSeverity.Error &&
        notifications.Messages[0].Detail.Contains("Проверьте сохранённый адрес") && !notifications.Messages[0].Detail.Contains("private-host"),
        "connection failure emits a safe error instead of a success notification");
    }

    notifications.Messages.Clear();
    var releaseProbe = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    service.Probe = _ => releaseProbe.Task;
    var closingPage = CreatePage(service, notifications);
    var probe = Call(closingPage, "CheckConnectionAsync");
    closingPage.Dispose(); releaseProbe.SetResult(); await probe;
    check(notifications.Messages.Count == 0, "late connection completion after page disposal emits no notification");

    foreach (var (value, expected) in new[] {
      (Result(), NotificationSeverity.Success),
      (Result(issues: 3), NotificationSeverity.Warning),
      (Result(fetch: false), NotificationSeverity.Error),
      (Result(analysis: false), NotificationSeverity.Error),
      (Result(analysis: false, error: ""), NotificationSeverity.Error),
      (Result(error: "Ошибка получения страниц."), NotificationSeverity.Error),
      (Result(cancelled: true, error: "Диагностика отменена."), NotificationSeverity.Info)
    })
    {
      notifications.Messages.Clear();
      var diagnostics = new DiagnosticStub { Run = _ => Task.FromResult(value) };
      using var panel = CreateDiagnostic(diagnostics, notifications);
      await Call(panel, "RunAsync");
      check(notifications.Messages.Count == 1 && notifications.Messages[0].Severity == expected &&
        !string.IsNullOrWhiteSpace(notifications.Messages[0].Detail) &&
        notifications.Messages[0].Style == NotificationStyle,
        $"diagnostic outcome has one correctly classified notification: fetch={value.FetchComplete}, analysis={value.AnalysisComplete}, issues={value.Issues}, cancelled={value.Cancelled}, error={value.Error}");
      check(ReferenceEquals(Field<ApiDiagnosticResult>(panel, "result"), value),
        "diagnostic notification preserves the detailed result and protocol identifier");
    }

    notifications.Messages.Clear();
    var failing = new DiagnosticStub { Run = _ => throw new IOException("private-host: secret diagnostic detail") };
    using (var panel = CreateDiagnostic(failing, notifications))
    {
      await Call(panel, "RunAsync");
      check(notifications.Messages.Count == 1 && notifications.Messages[0].Severity == NotificationSeverity.Error &&
        !notifications.Messages[0].Detail.Contains("private-host"), "diagnostic exception emits a safe error notification");
    }

    notifications.Messages.Clear();
    var waiting = new DiagnosticStub { Run = WaitForCancellation };
    using (var panel = CreateDiagnostic(waiting, notifications))
    {
      var running = Call(panel, "RunAsync");
      Invoke(panel, "Cancel"); await running;
      check(notifications.Messages.Count == 1 && notifications.Messages[0].Severity == NotificationSeverity.Info && waiting.Token.IsCancellationRequested,
        "explicit diagnostic cancellation reaches the service and emits one informational notification");
    }

    notifications.Messages.Clear();
    var closingPanel = CreateDiagnostic(waiting, notifications);
    var closingRun = Call(closingPanel, "RunAsync");
    closingPanel.Dispose(); await closingRun;
    check(notifications.Messages.Count == 0, "disposing an active diagnostic panel cancels work without a late notification");
  }

  private static ApiDiagnosticResult Result(bool fetch = true, bool analysis = true, int issues = 0, bool cancelled = false, string? error = null) =>
    new(Guid.NewGuid(), fetch, analysis, cancelled, 1, 2, 3, issues, error, 4);

  private static async Task<ApiDiagnosticResult> WaitForCancellation(CancellationToken ct)
  {
    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    throw new InvalidOperationException("Cancellation wait unexpectedly completed.");
  }

  private static Page CreatePage(SyncStub service, NotificationService notifications)
  {
    var page = new Page();
    Inject(page, "Service", service); Inject(page, "Notifications", notifications);
    Inject(page, "Logger", NullLogger<Page>.Instance);
    Inject(page, "Access", new UserAccessSnapshot("feedback-root", true, []));
    return page;
  }
  private static DiagnosticPanel CreateDiagnostic(DiagnosticStub service, NotificationService notifications)
  {
    var panel = new DiagnosticPanel();
    Inject(panel, "Service", service); Inject(panel, "Notifications", notifications);
    Inject(panel, "Logger", NullLogger<DiagnosticPanel>.Instance);
    Inject(panel, "Access", new UserAccessSnapshot("feedback-root", true, []));
    Field<DiagnosticPanel.DiagnosticForm>(panel, "form").UpdatedAt = new DateTime(2026, 10, 1);
    return panel;
  }
  private static void Inject(object component, string name, object value) =>
    component.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(component, value);
  private static T Field<T>(object component, string name) =>
    (T)component.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(component)!;
  private static object? Invoke(object component, string name) =>
    component.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component, null);
  private static Task Call(object component, string name) => (Task)Invoke(component, name)!;

  private sealed class DiagnosticStub : IApiDiagnosticService
  {
    public Func<CancellationToken, Task<ApiDiagnosticResult>> Run { get; init; } = default!;
    public CancellationToken Token { get; private set; }
    public Task<ApiDiagnosticResult> RunAsync(DateOnly updatedSince, CancellationToken ct = default, IProgress<string>? progress = null)
    { Token = ct; return Run(ct); }
  }
  private sealed class SyncStub : ISynchronizationService
  {
    public Func<CancellationToken, Task> Probe { get; set; } = _ => Task.CompletedTask;
    public Task CheckConnectionAsync(CancellationToken ct = default) => Probe(ct);
    public Task<SynchronizationOverview> GetOverviewAsync(bool classifiers = false, bool includeChildren = true, CancellationToken ct = default, bool allSubdivisions = false) => throw new NotSupportedException();
    public Task<IReadOnlyList<SynchronizationRunDto>> GetHistoryAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<DirectoryActuality> GetActualityAsync(string readPermission, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<SynchronizationRunDto> RunAsync(CancellationToken ct = default, DateOnly? updatedSince = null, bool enqueue = false) => throw new NotSupportedException();
    public Task SaveSettingsAsync(DataManagementMode mode, bool enabled, TimeOnly time, Guid version, CancellationToken ct = default, string? apiBaseUrl = null, int? apiPageSize = null, string? apiUsername = null, string? apiPassword = null, long? apiHeadSubdivisionId = null, bool? completeSnapshotGuaranteed = null) => throw new NotSupportedException();
  }
}
