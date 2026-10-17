using System.Reflection;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Radzen;
using IpIndex = Guard.Components.Pages.IpAddresses.Index;

internal static class IpSearchUiTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var service = DispatchProxy.Create<IIpAddressManagementService, IpAddressReferenceTests.ServiceProxy>();
    var pending = new Queue<TaskCompletionSource<IpSearchResult>>();
    var tokens = new List<CancellationToken>();
    ((IpAddressReferenceTests.ServiceProxy)(object)service).Handle = (method, arguments) => {
      if (method.Name != nameof(IIpAddressManagementService.SearchAsync)) throw new NotSupportedException(method.Name);
      var request = (IpSearchRequest)arguments[0]!;
      check(request.Skip >= 0 && request.Take == 20, "IP UI sends bounded search parameters");
      tokens.Add((CancellationToken)arguments[1]!);
      var result = new TaskCompletionSource<IpSearchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
      pending.Enqueue(result); return result.Task;
    };
    using var index = new Probe(service);
    var first = index.Load(); var old = pending.Dequeue();
    var second = index.Load(); var current = pending.Dequeue();
    check(tokens[0].IsCancellationRequested, "new IP UI load cancels previous search");
    var id = Guid.NewGuid();
    current.SetResult(new([new() { Id = id, Address = "192.0.2.1" }], 1)); await second;
    old.SetResult(new([new() { Id = Guid.NewGuid(), Address = "192.0.2.2" }], 999)); await first;
    check(index.Ids.SequenceEqual([id]) && !index.Loading, "late IP search response cannot replace current page or loading state");
    var last = index.Load(); var late = pending.Dequeue(); index.Dispose();
    late.SetResult(new([], 0)); await last;
    check(tokens[^1].IsCancellationRequested && index.Ids.SequenceEqual([id]), "disposed IP list cancels search and ignores late response");
  }

  private sealed class Probe : IpIndex
  {
    public Probe(IIpAddressManagementService service) {
      IpAddressService = service; NotificationService = new(); Logger = NullLogger<IpIndex>.Instance;
    }
    public Task Load() => (Task)typeof(IpIndex).GetMethod("LoadData", BindingFlags.NonPublic | BindingFlags.Instance)!
      .Invoke(this, [new LoadDataArgs { Skip = 0, Top = 20 }])!;
    public IEnumerable<Guid> Ids => filteredData.Select(row => row.Id);
    public bool Loading => isLoading;
  }
}
