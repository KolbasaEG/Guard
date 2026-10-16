using System.Reflection;
using System.Text.Json;
using Guard.Components.Library;
using Guard.Components.Pages.Classifiers;
using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Guard.Core.Services.Exports;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

internal static class ClassifierUiTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var time = new BrowserTimeService(new Js()); await time.InitializeAsync();
#pragma warning disable BL0005
    var descriptor = new CompositeFilterDescriptor { Property = "Value", FilterOperator = FilterOperator.Contains, FilterValue = "Alpha" };
    var filter = new RadzenDataFilter<Classifier> { Filters = [descriptor] };
#pragma warning restore BL0005
    var snapshot = ClassifierListQuery.Capture(filter, time); descriptor.FilterValue = "Beta";
    check(ClassifierSearchQuery.Filter(new[] { new Classifier { Value = "Alpha" }, new Classifier { Value = "Beta" } }.AsQueryable(), snapshot)
      .Single().Value == "Alpha", "classifier applied filter is independent of edited controls");
    foreach (var invalid in new ExportFilter[] {
      new("Subdivisions.Count", "Equals", JsonSerializer.SerializeToElement(1)), new("Code", "Contains", JsonSerializer.SerializeToElement(1)),
      new("Value", "Contains", JsonSerializer.SerializeToElement(1)), new("IsActive", "GreaterThan", JsonSerializer.SerializeToElement(true)),
      new("UpdatedAt", "Equals", JsonSerializer.SerializeToElement(DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified))),
      new(Children: Enumerable.Repeat(new ExportFilter("Value", "IsNull"), 101).ToArray())
    }) await PostgresTests.ThrowsAsync<ArgumentException>(() => Task.Run(() => ClassifierSearchQuery.ValidateFilter(invalid)), check, "classifier filter rejects invalid field, type, operator or complexity");

    var service = DispatchProxy.Create<IClassifierService, IpAddressReferenceTests.ServiceProxy>();
    var proxy = (IpAddressReferenceTests.ServiceProxy)(object)service;
    var pending = new Queue<TaskCompletionSource<ClassifierSearchResult>>(); var tokens = new List<CancellationToken>();
    proxy.Handle = (method, args) => {
      if (method.Name != nameof(IClassifierService.SearchAsync)) throw new NotSupportedException(method.Name);
      tokens.Add((CancellationToken)args[1]!);
      var completion = new TaskCompletionSource<ClassifierSearchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
      pending.Enqueue(completion); return completion.Task;
    };
    using var page = new PageProbe(service, time);
    var first = page.Load(); var old = pending.Dequeue(); var second = page.Load(); var current = pending.Dequeue();
    current.SetResult(new([new(1, Guid.NewGuid(), 1, 1, "Test", "Current", true, DateTime.UtcNow)], 1)); await second;
    old.SetResult(new([], 999)); await first;
    check(tokens[0].IsCancellationRequested && page.Ids.SequenceEqual([1]), "classifier page ignores cancelled late search");
    var third = page.Load(); pending.Dequeue().SetException(new IOException()); await third;
    check(!page.Ids.Any(), "classifier search failure clears stale results");

    using var dialogs = new DialogService(new Navigation(), new Js()); var notifications = new NotificationService(); var closes = 0;
    dialogs.OnClose += _ => closes++;
    using var add = new AddProbe(service, dialogs, notifications);
    var saving = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously); var calls = 0;
    CreateClassifierDto? command = null; CancellationToken token = default;
    proxy.Handle = (_, args) => { calls++; command = (CreateClassifierDto)args[0]!; token = (CancellationToken)args[1]!; return saving.Task; };
    var dialog = dialogs.OpenAsync<Add>(""); add.Value = "Before";
    var save = add.Save(); await add.Save(); add.Value = "After";
    check(calls == 1 && command?.Fields.Value == "Before", "classifier form snapshots fields and blocks duplicate submission");
    add.Cancel(); await dialog; var next = dialogs.OpenAsync<Add>(""); saving.SetResult(1); await save;
    check(token.IsCancellationRequested && closes == 1 && notifications.Messages.Count == 0, "late classifier save cannot close following dialog");
    dialogs.Close(null); await next;
    var loading = new TaskCompletionSource<ClassifierDetailsDto?>(TaskCreationOptions.RunContinuationsAsynchronously);
    proxy.Handle = (_, _) => loading.Task;
    using var edit = new EditProbe(service, dialogs, notifications); var initialize = edit.Initialize(); edit.Dispose(); edit.Dispose();
    loading.SetResult(new() { Value = "Late" }); await initialize;
    check(!edit.Loaded, "disposed classifier editor ignores late card response");
  }

  private sealed class PageProbe : Guard.Components.Pages.Classifiers.Index
  {
    public PageProbe(IClassifierService service, BrowserTimeService time) { ClassifierService = service; Time = time; NotificationService = new(); Logger = NullLogger<Guard.Components.Pages.Classifiers.Index>.Instance; }
    public Task Load() => LoadData(new() { Skip = 0, Top = 20 });
    public IEnumerable<int> Ids => filteredData.Select(c => c.Id);
  }
  private sealed class AddProbe : Add
  {
    public AddProbe(IClassifierService service, DialogService dialogs, NotificationService notifications) { ClassifierService = service; DialogService = dialogs; NotificationService = notifications; Logger = NullLogger<Add>.Instance; }
    public string Value { set => item.Value = value; }
    public Task Save() => FormSubmit();
    public void Cancel() => HandleCancelButtonClick();
  }
  private sealed class EditProbe : Edit
  {
    public EditProbe(IClassifierService service, DialogService dialogs, NotificationService notifications) { ClassifierService = service; DialogService = dialogs; NotificationService = notifications; Logger = NullLogger<Edit>.Instance; }
    public bool Loaded => isLoaded;
    public Task Initialize() => OnInitializedAsync();
  }
  private sealed class Navigation : NavigationManager { public Navigation() => Initialize("https://guard.test/", "https://guard.test/"); }
  private sealed class Js : IJSRuntime, IJSObjectReference
  {
    public ValueTask<T> InvokeAsync<T>(string id, object?[]? args) => ValueTask.FromResult((T)(id == "import" ? (object)this : "Europe/Moscow"));
    public ValueTask<T> InvokeAsync<T>(string id, CancellationToken ct, object?[]? args) => InvokeAsync<T>(id, args);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }
}
