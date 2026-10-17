using System.Reflection;
using Guard.Components.Pages.IpAddresses;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

internal static class IpAddressReferenceTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var own = new Subdivision { Path = "/1/", Name = "Own" };
    var child = new Subdivision { Path = "/1/2/", Name = "Child" };
    var foreign = new Subdivision { Path = "/3/", Name = "Foreign" };
    var rows = new[] {
      new IpAddress { Address = "192.0.2.1", Subdivision = own },
      new IpAddress { Address = "192.0.2.2", Subdivision = child },
      new IpAddress { Address = "192.0.2.3", Subdivision = foreign },
      new IpAddress { Address = "2001:db8::/64" },
      new IpAddress { Address = "192.0.2.4", Subdivision = own, Status = Status.Blocked },
      new IpAddress { Address = "192.0.2.5", Subdivision = own, Status = Status.Archived }
    }.AsQueryable();
    IQueryable<IpAddress> Visible(bool all, string? path, SubdivisionHierarchyMode hierarchy) =>
      IpSearchQuery.ApplyVisibility(rows, DataViewMode.Active, all, true, path, hierarchy);
    check(Visible(true, null, SubdivisionHierarchyMode.CurrentOnly).Count() == 5, "Root all-IP mode includes common and blocked addresses without own subdivision");
    check(Visible(false, "/1/", SubdivisionHierarchyMode.CurrentOnly).Count() == 2, "Root own-IP mode narrows full server access");
    check(Visible(false, "/1/", SubdivisionHierarchyMode.IncludeChildren).Count() == 3, "IP descendants mode excludes common and foreign addresses");
    foreach (var path in new string?[] { null, "", "/", "/oops/" })
      check(!Visible(false, path, SubdivisionHierarchyMode.IncludeChildren).Any(), "invalid own path produces empty IP display scope");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => Task.FromResult(
      IpSearchQuery.ApplyVisibility(rows, DataViewMode.Active, true, false, "/1/", SubdivisionHierarchyMode.CurrentOnly)), check, "operator cannot request all-IP display scope");
#pragma warning disable BL0005
    var condition = new CompositeFilterDescriptor { Property = "Address", FilterOperator = FilterOperator.Contains, FilterValue = "192.0.2." };
    var filter = new RadzenDataFilter<IpAddress> { Filters = [condition] };
    var applied = IpListQuery.Capture(filter);
    condition.FilterValue = "2001:";
    check(IpSearchQuery.Filter(rows, applied).Count() == 5, "editing IP filter leaves its applied snapshot unchanged");
    var cyclic = new CompositeFilterDescriptor(); cyclic.Filters = [cyclic]; filter.Filters = [cyclic];
    await PostgresTests.ThrowsAsync<ArgumentException>(() => Task.FromResult(IpListQuery.Capture(filter)), check, "cyclic IP filter is bounded");
#pragma warning restore BL0005
    var service = DispatchProxy.Create<IIpAddressManagementService, ServiceProxy>();
    var stub = (ServiceProxy)(object)service;
    var notifications = new NotificationService();
    using var dialogs = new DialogService(new TestNavigation(), new NoJs());
    var closes = 0; dialogs.OnClose += _ => closes++;
    var create = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
    var calls = 0; CancellationToken token = default;
    CreateIpAddressDto? submitted = null;
    stub.Handle = (_, args) => { calls++; submitted = (CreateIpAddressDto)args[0]!; token = (CancellationToken)args[1]!; return create.Task; };
    var creatingDialog = dialogs.OpenAsync<Add>("");
    using (var add = new AddProbe(service, notifications, dialogs))
    {
      var pending = add.Save(); await add.Save();
      check(calls == 1 && add.Saving, "IP create rejects duplicate submissions");
      add.Address = "changed after submit";
      check(submitted?.Address == "", "IP create snapshots only editable scalar fields before async save");
      add.Cancel(); await creatingDialog;
      var followingDialog = dialogs.OpenAsync<Add>("");
      create.SetResult(Guid.NewGuid()); await pending;
      check(token.IsCancellationRequested && closes == 1 && notifications.Messages.Count == 0,
        "cancelled IP creation cannot close a subsequent dialog or notify late");
      dialogs.Close(null); await followingDialog; closes = 0;
    }
    var load = new TaskCompletionSource<IpAddressDetailsDto?>(TaskCreationOptions.RunContinuationsAsynchronously);
    stub.Handle = (_, args) => { token = (CancellationToken)args[1]!; return load.Task; };
    var edit = new EditProbe(service, notifications, dialogs);
    var initializing = edit.Initialize(); edit.Dispose();
    load.SetResult(new IpAddressDetailsDto { Address = "192.0.2.9" }); await initializing;
    check(!edit.Loaded && token.IsCancellationRequested && closes == 0, "disposed IP dialog ignores late entity load");
    var update = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var updates = 0;
    stub.Handle = (method, _) => method.Name switch {
      nameof(IIpAddressManagementService.GetDetailsAsync) => Task.FromResult<IpAddressDetailsDto?>(new() { Address = "2001:db8::/64" }),
      nameof(IIpAddressManagementService.UpdateAsync) => Save(),
      nameof(IIpAddressManagementService.GetSubdivisionOptionsAsync) => Task.FromResult<IReadOnlyList<IpSubdivisionOptionDto>>([]),
      _ => throw new NotSupportedException(method.Name)
    };
    Task Save() { updates++; return update.Task; }
    using var retry = new EditProbe(service, notifications, dialogs);
    var editingDialog = dialogs.OpenAsync<Edit>("");
    await retry.Initialize(); var saving = retry.Save(); await retry.Save();
    check(updates == 1 && retry.Saving, "IP update rejects duplicate submissions independently of loading");
    update.SetException(new InvalidOperationException("internal database detail")); await saving;
    check(!retry.Saving && retry.Address == "2001:db8::/64" && notifications.Messages.Count == 1 &&
      !notifications.Messages[0].Detail.Contains("internal database detail"), "failed IP update retains fields and hides internal exceptions");
    update = new(TaskCreationOptions.RunContinuationsAsynchronously);
    var retrying = retry.Save(); update.SetResult(); await retrying;
    await editingDialog;
    check(updates == 2 && closes == 1, "IP update can be retried after failure");
    var options = new TaskCompletionSource<IReadOnlyList<IpSubdivisionOptionDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
    stub.Handle = (_, args) => { token = (CancellationToken)args[0]!; return options.Task; };
    var loadingOptions = new AddProbe(service, notifications, dialogs);
    var initializingOptions = loadingOptions.Initialize();
    await loadingOptions.Save();
    check(loadingOptions.Loading && !loadingOptions.Saving, "IP creation cannot save during option loading");
    loadingOptions.Dispose(); options.SetResult([new(Guid.NewGuid(), "Late")]); await initializingOptions;
    check(token.IsCancellationRequested && loadingOptions.OptionCount == 0, "disposed IP form ignores late subdivision options");
  }
  public class ServiceProxy : DispatchProxy
  {
    public Func<MethodInfo, object?[], object?> Handle { get; set; } = default!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handle(method!, args ?? []);
  }
  private sealed class AddProbe : Add
  {
    public AddProbe(IIpAddressManagementService service, NotificationService notifications, DialogService dialogs)
    { IpAddressService = service; NotificationService = notifications; DialogService = dialogs; Logger = NullLogger<Add>.Instance; }
    public Task Save() => FormSubmit();
    public void Cancel() => HandleCancelButtonClick();
    public bool Saving => isSaving;
    public Task Initialize() => OnInitializedAsync();
    public bool Loading => isLoading;
    public int OptionCount => subdivisions.Count();
    public string Address { set => item.Address = value; }
  }
  private sealed class EditProbe : Edit
  {
    public EditProbe(IIpAddressManagementService service, NotificationService notifications, DialogService dialogs)
    { IpAddressService = service; NotificationService = notifications; DialogService = dialogs; Logger = NullLogger<Edit>.Instance; }
    public Task Initialize() => OnInitializedAsync();
    public Task Save() => FormSubmit();
    public bool Saving => isSaving;
    public bool Loaded => isLoaded;
    public string Address => item.Address;
  }
  private sealed class NoJs : IJSRuntime
  {
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
  }
  private sealed class TestNavigation : NavigationManager
  {
    public TestNavigation() => Initialize("https://guard.test/", "https://guard.test/");
    protected override void NavigateToCore(string uri, bool forceLoad) { }
  }
}
