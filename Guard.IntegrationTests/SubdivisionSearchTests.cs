using System.Reflection;
using System.Text.Json;
using Guard.Components.Library;
using Guard.Components.Pages.Subdivisions;
using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Guard.Core.Services.Exports;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

internal static class SubdivisionSearchTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var time = new BrowserTimeService(new Js()); await time.InitializeAsync();
#pragma warning disable BL0005
    var descriptor = new CompositeFilterDescriptor { Property = "Name", FilterOperator = FilterOperator.Contains, FilterValue = "Alpha" };
    var filter = new RadzenDataFilter<Subdivision> { Filters = [descriptor] };
    var snapshot = SubdivisionListQuery.Capture(filter, time); descriptor.FilterValue = "Beta";
    var rows = new[] { new Subdivision { Name = "Alpha" }, new Subdivision { Name = "Beta" } }.AsQueryable();
    check(SubdivisionSearchQuery.Filter(rows, snapshot).Single().Name == "Alpha", "subdivision applied filter is independent of constructor");
    var cyclic = new CompositeFilterDescriptor(); cyclic.Filters = [cyclic]; filter.Filters = [cyclic];
    await PostgresTests.ThrowsAsync<ArgumentException>(() => Task.FromResult(SubdivisionListQuery.Capture(filter, time)), check, "cyclic subdivision filter rejected before date conversion");
    var date = new DateTime(2026, 10, 16);
    filter.Filters = [new() { Property = "InsertedDate", FilterOperator = FilterOperator.Equals, FilterValue = date }];
    var dateFilter = SubdivisionListQuery.Capture(filter, time);
    check(SubdivisionSearchQuery.Filter(new[] { new Subdivision { InsertedDate = time.ToUtc(date.AddHours(2)) },
      new Subdivision { InsertedDate = time.ToUtc(date.AddDays(1)) } }.AsQueryable(), dateFilter).Count() == 1,
      "subdivision browser date becomes UTC day range");
#pragma warning restore BL0005
    foreach (var invalid in new[] {
      new ExportFilter("Personals.Count", "Equals", JsonSerializer.SerializeToElement(1)),
      new("Name", "Contains", JsonSerializer.SerializeToElement(12)),
      new("StaffCount", "Contains", JsonSerializer.SerializeToElement(1)),
      new("LevelOrder", "Equals", JsonSerializer.SerializeToElement("1")),
      new("IsDepartment", "GreaterThan", JsonSerializer.SerializeToElement(true)),
      new("InsertedDate", "Equals", JsonSerializer.SerializeToElement(date)),
      new("Name", "In", JsonSerializer.SerializeToElement(new[] { 1 })),
      new(Children: Enumerable.Repeat(new ExportFilter("Name", "IsNull"), 101).ToArray())
    }) await PostgresTests.ThrowsAsync<ArgumentException>(() => Task.Run(() => SubdivisionSearchQuery.ValidateFilter(invalid)), check, "subdivision filter validates field, operation, value and complexity");
    var service = DispatchProxy.Create<ISubdivisionManagementService, IpAddressReferenceTests.ServiceProxy>();
    var pending = new Queue<TaskCompletionSource<SubdivisionSearchResult>>(); var tokens = new List<CancellationToken>();
    ((IpAddressReferenceTests.ServiceProxy)(object)service).Handle = (method, args) => {
      if (method.Name != nameof(ISubdivisionManagementService.SearchAsync)) throw new NotSupportedException(method.Name);
      var request = (SubdivisionSearchRequest)args[0]!;
      check(request.Skip == 0 && request.Take == 20, "subdivision UI sends explicit search parameters");
      tokens.Add((CancellationToken)args[1]!);
      var completion = new TaskCompletionSource<SubdivisionSearchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
      pending.Enqueue(completion); return completion.Task;
    };
    using var page = new Probe(service, time);
    var first = page.Load(); var old = pending.Dequeue();
    var second = page.Load(); var current = pending.Dequeue();
    check(tokens[0].IsCancellationRequested, "subdivision replacement load cancels previous search");
    var id = Guid.NewGuid(); current.SetResult(new([new() { Id = id, Name = "Current" }], 1)); await second;
    old.SetResult(new([], 999)); await first;
    check(page.Ids.SequenceEqual([id]) && !page.Loading, "late subdivision page cannot overwrite current result");
    var third = page.Load(); var failed = pending.Dequeue(); failed.SetException(new IOException()); await third;
    check(!page.Ids.Any() && !page.Loading, "failed subdivision search clears untrusted page");
    var fourth = page.Load(); var late = pending.Dequeue(); page.Dispose(); late.SetResult(new([new() { Id = id }], 1)); await fourth;
    check(tokens[^1].IsCancellationRequested && !page.Ids.Any(), "disposed subdivision page ignores late result");
  }
  private sealed class Probe : Guard.Components.Pages.Subdivisions.Index
  {
    public Probe(ISubdivisionManagementService service, BrowserTimeService time) {
      SubdivisionService = service; Time = time; NotificationService = new(); Logger = NullLogger<Guard.Components.Pages.Subdivisions.Index>.Instance;
    }
    public Task Load() => LoadData(new() { Skip = 0, Top = 20 });
    public IEnumerable<Guid> Ids => filteredData.Select(s => s.Id);
    public bool Loading => isLoading;
  }
  private sealed class Js : IJSRuntime, IJSObjectReference
  {
    public ValueTask<T> InvokeAsync<T>(string id, object?[]? args) => ValueTask.FromResult((T)(id == "import" ? (object)this : "Europe/Moscow"));
    public ValueTask<T> InvokeAsync<T>(string id, CancellationToken ct, object?[]? args) => InvokeAsync<T>(id, args);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }
}
