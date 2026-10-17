using System.Reflection;
using Guard.Components.Library;
using Guard.Components.Pages.Logs;
using Guard.Components.Pages.Roles;
using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

internal static class AdministrationSearchUiTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var time = new BrowserTimeService(new Js()); await time.InitializeAsync();
#pragma warning disable BL0005
    var descriptor = new CompositeFilterDescriptor { Property = "Name", FilterOperator = FilterOperator.Contains, FilterValue = "Alpha" };
    var filter = new RadzenDataFilter<RoleFilterFields> { Filters = [descriptor] };
    var levels = new[] { "Error" };
    var levelDescriptor = new CompositeFilterDescriptor { Property = "Level", FilterOperator = FilterOperator.In, FilterValue = levels };
    var logFilter = new RadzenDataFilter<LogFilterFields> { Filters = [levelDescriptor] };
#pragma warning restore BL0005
    var snapshot = AdministrationListQuery.Capture(filter, time, RoleSearchFilter.Fields, RoleSearchFilter.Validate);
    descriptor.FilterValue = "Beta";
    check(RoleSearchQuery.Filter(new[] { new ApplicationRole { Name = "Alpha" }, new ApplicationRole { Name = "Beta" } }.AsQueryable(), snapshot).Single().Name == "Alpha", "role applied filter is independent of edits");
    var logSnapshot = AdministrationListQuery.Capture(logFilter, time, LogSearchFilter.Fields, LogSearchFilter.Validate);
    levels[0] = "Information";
    check(LogSearchQuery.Filter(new[] { new LogEntry { Level = "Error" }, new LogEntry { Level = "Information" } }.AsQueryable(), logSnapshot).Single().Level == "Error", "log selection snapshot does not share mutable arrays");

    var roles = DispatchProxy.Create<IRoleSearchService, IpAddressReferenceTests.ServiceProxy>();
    var queue = new Queue<TaskCompletionSource<RoleSearchResult>>(); var tokens = new List<CancellationToken>();
    ((IpAddressReferenceTests.ServiceProxy)(object)roles).Handle = (_, args) => {
      tokens.Add((CancellationToken)args[1]!); var completion = new TaskCompletionSource<RoleSearchResult>(); queue.Enqueue(completion); return completion.Task;
    };
    using var page = new RolePage(roles, time);
    var first = page.Load(); var old = queue.Dequeue(); var second = page.Load();
    queue.Dequeue().SetResult(new([new("current", "Current", "CURRENT")], 1)); await second;
    old.SetResult(new([], 999)); await first;
    check(tokens[0].IsCancellationRequested && page.Ids.SequenceEqual(["current"]), "role list ignores late cancelled response");
    var failed = page.Load(); queue.Dequeue().SetException(new IOException()); await failed;
    check(!page.Ids.Any(), "role load error clears stale data");

    var logs = DispatchProxy.Create<ILogSearchService, IpAddressReferenceTests.ServiceProxy>();
    var logQueue = new Queue<TaskCompletionSource<LogSearchResult>>(); tokens.Clear();
    ((IpAddressReferenceTests.ServiceProxy)(object)logs).Handle = (_, args) => {
      tokens.Add((CancellationToken)args[1]!); var completion = new TaskCompletionSource<LogSearchResult>(); logQueue.Enqueue(completion); return completion.Task;
    };
    await using var logPage = new LogPage(logs, time);
    first = logPage.Load(); var previous = logQueue.Dequeue(); second = logPage.Load();
    logQueue.Dequeue().SetResult(new([new(DateTime.UtcNow, "Error", "Service", "current", null, null, null)], 1)); await second;
    previous.SetResult(new([], 999)); await first;
    check(tokens[0].IsCancellationRequested && logPage.Messages.SequenceEqual(["current"]), "log list ignores late cancelled response");
    failed = logPage.Load(); logQueue.Dequeue().SetException(new IOException()); await failed;
    check(!logPage.Messages.Any(), "log load error clears stale data");
    var late = logPage.Load(); await logPage.DisposeAsync();
    logQueue.Dequeue().SetResult(new([], 999)); await late;
    check(tokens[^1].IsCancellationRequested && !logPage.Messages.Any(), "log disposal cancels pending request and ignores completion");
  }

  private sealed class RolePage : Guard.Components.Pages.Roles.Index
  {
    public RolePage(IRoleSearchService service, BrowserTimeService time) { RoleService = service; Time = time; PermissionService = new AdministrationSearchIntegrationTests.PermissionsStub(); NotificationService = new(); Logger = NullLogger<Guard.Components.Pages.Roles.Index>.Instance; }
    public Task Load() => LoadData(new() { Skip = 0, Top = 10 });
    public IEnumerable<string> Ids => filteredData.Select(r => r.Id);
  }
  private sealed class LogPage : Guard.Components.Pages.Logs.Index
  {
    public LogPage(ILogSearchService service, BrowserTimeService time) { LogService = service; Time = time; Permissions = new AdministrationSearchIntegrationTests.PermissionsStub(); NotificationService = new(); Logger = NullLogger<Guard.Components.Pages.Logs.Index>.Instance; }
    public Task Load() => LoadData(new() { Skip = 0, Top = 10 });
    public IEnumerable<string?> Messages => filteredData.Select(r => r.Message);
  }
  private sealed class Js : IJSRuntime, IJSObjectReference
  {
    public ValueTask<T> InvokeAsync<T>(string id, object?[]? args) => ValueTask.FromResult((T)(id == "import" ? (object)this : "Europe/Moscow"));
    public ValueTask<T> InvokeAsync<T>(string id, CancellationToken ct, object?[]? args) => InvokeAsync<T>(id, args);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }
}
