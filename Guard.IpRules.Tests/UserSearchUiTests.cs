using System.Reflection;
using System.Text.Json;
using Guard.Components.Library;
using Guard.Components.Pages.Users;
using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Guard.Core.Services.Exports;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

internal static class UserSearchUiTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var time = new BrowserTimeService(new Js()); await time.InitializeAsync();
#pragma warning disable BL0005
    var descriptor = new CompositeFilterDescriptor { Property = "UserName", FilterOperator = FilterOperator.Contains, FilterValue = "Alpha" };
    var filter = new RadzenDataFilter<UserFilterFields> { Filters = [descriptor] };
#pragma warning restore BL0005
    var snapshot = UserListQuery.Capture(filter, time); descriptor.FilterValue = "Beta";
    check(UserSearchQuery.Filter(new[] { new ApplicationUser { UserName = "Alpha" }, new ApplicationUser { UserName = "Beta" } }.AsQueryable(), snapshot)
      .Single().UserName == "Alpha", "user applied filter is independent of edited controls");
    foreach (var invalid in new ExportFilter[] {
      new("Subdivisions.Count", "Equals", JsonSerializer.SerializeToElement(1)), new("PersonalId", "Contains", JsonSerializer.SerializeToElement(1)),
      new("UserName", "Contains", JsonSerializer.SerializeToElement(1)), new("MustChangePassword", "GreaterThan", JsonSerializer.SerializeToElement(true)),
      new("LastActivityAtUtc", "Equals", JsonSerializer.SerializeToElement(DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified))),
      new(Children: Enumerable.Repeat(new ExportFilter("UserName", "IsNull"), 101).ToArray())
    }) await PostgresTests.ThrowsAsync<ArgumentException>(() => Task.Run(() => UserSearchQuery.ValidateFilter(invalid)), check, "user filter rejects invalid field, type, operator or complexity");

    var service = DispatchProxy.Create<ISecurityService, IpAddressReferenceTests.ServiceProxy>();
    var proxy = (IpAddressReferenceTests.ServiceProxy)(object)service;
    var pending = new Queue<TaskCompletionSource<UserSearchResult>>(); var tokens = new List<CancellationToken>();
    proxy.Handle = (method, args) => {
      if (method.Name != nameof(ISecurityService.SearchUsersAsync)) throw new NotSupportedException(method.Name);
      tokens.Add((CancellationToken)args[1]!);
      var completion = new TaskCompletionSource<UserSearchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
      pending.Enqueue(completion); return completion.Task;
    };
    using var page = new PageProbe(service, time);
    var first = page.Load(); var old = pending.Dequeue(); var second = page.Load(); var current = pending.Dequeue();
    current.SetResult(new([new UserDto { Id = "current" }], 1)); await second;
    old.SetResult(new([], 999)); await first;
    check(tokens[0].IsCancellationRequested && page.Ids.SequenceEqual(["current"]), "user page ignores cancelled late search");
    var third = page.Load(); pending.Dequeue().SetException(new IOException()); await third;
    check(!page.Ids.Any(), "user search failure clears stale results");

  }

  private sealed class PageProbe : Guard.Components.Pages.Users.Index
  {
    public PageProbe(ISecurityService service, BrowserTimeService time) { Security = service; Time = time; NotificationService = new(); Logger = NullLogger<Guard.Components.Pages.Users.Index>.Instance; }
    public Task Load() => LoadData(new() { Skip = 0, Top = 20 });
    public IEnumerable<string> Ids => filteredData.Select(c => c.Id);
  }
  private sealed class Js : IJSRuntime, IJSObjectReference
  {
    public ValueTask<T> InvokeAsync<T>(string id, object?[]? args) => ValueTask.FromResult((T)(id == "import" ? (object)this : "Europe/Moscow"));
    public ValueTask<T> InvokeAsync<T>(string id, CancellationToken ct, object?[]? args) => InvokeAsync<T>(id, args);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }
}
