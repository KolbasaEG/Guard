using System.Reflection;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Radzen;
using Pages = Guard.Components.Pages.Subdivisions;

internal static class SubdivisionDialogLifecycleTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var service = DispatchProxy.Create<ISubdivisionService, IpAddressReferenceTests.ServiceProxy>();
    var proxy = (IpAddressReferenceTests.ServiceProxy)(object)service;
    using var dialogs = new DialogService(new Navigation(), new NoJs());
    var notifications = new NotificationService(); var closes = 0;
    dialogs.OnClose += _ => closes++;
    proxy.Handle = (method, _) => method.Name == nameof(ISubdivisionService.GetEditorOptionsAsync)
      ? Task.FromResult(new SubdivisionEditorOptionsDto([], [], [])) : throw new NotSupportedException(method.Name);
    using var add = new AddProbe(service, dialogs, notifications);
    await add.Initialize();
    var saving = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
    CreateSubdivisionDto? command = null; var calls = 0; CancellationToken token = default;
    proxy.Handle = (_, args) => { calls++; command = (CreateSubdivisionDto)args[0]!; token = (CancellationToken)args[1]!; return saving.Task; };
    var dialog = dialogs.OpenAsync<Pages.Add>("");
    add.Name = "До отправки";
    var pending = add.Save(); await add.Save(); add.Name = "После отправки";
    check(calls == 1 && command?.Fields.Name == "До отправки", "subdivision create blocks double submit and snapshots editable fields");
    add.Cancel(); await dialog;
    var next = dialogs.OpenAsync<Pages.Add>("");
    saving.SetResult(Guid.NewGuid()); await pending;
    check(token.IsCancellationRequested && closes == 1 && notifications.Messages.Count == 0,
      "late subdivision create cannot close the following dialog or notify");
    dialogs.Close(null); await next; closes = 0;

    var loading = new TaskCompletionSource<SubdivisionDetailsDto?>(TaskCreationOptions.RunContinuationsAsynchronously);
    proxy.Handle = (_, args) => { token = (CancellationToken)args[1]!; return loading.Task; };
    var edit = new EditProbe(service, dialogs, notifications);
    var initializing = edit.Initialize(); edit.Dispose(); edit.Dispose();
    loading.SetResult(new() { Name = "Late" }); await initializing;
    check(!edit.Loaded && token.IsCancellationRequested && closes == 0, "disposed subdivision editor ignores late details and supports repeated dispose");

    proxy.Handle = (method, _) => method.Name == nameof(ISubdivisionService.GetDetailsAsync)
      ? Task.FromResult<SubdivisionDetailsDto?>(new() { Id = Guid.NewGuid(), Name = "Move" })
      : Task.FromException<IReadOnlyList<SubdivisionParentOptionDto>>(new UnauthorizedAccessException());
    using var move = new MoveProbe(service, dialogs, notifications);
    dialog = dialogs.OpenAsync<Pages.Move>("");
    await move.Initialize(); await dialog;
    check(!move.Loaded && notifications.Messages.Count == 1 && closes == 1,
      "failed move options prevent saving an incompletely loaded subdivision dialog");
  }

  private sealed class AddProbe : Pages.Add
  {
    public AddProbe(ISubdivisionService service, DialogService dialogs, NotificationService notifications) {
      SubdivisionService = service; DialogService = dialogs; NotificationService = notifications; Logger = NullLogger<Pages.Add>.Instance;
    }
    public string Name { set => item.Name = value; }
    public Task Initialize() => OnInitializedAsync();
    public Task Save() => FormSubmit();
    public void Cancel() => HandleCancelButtonClick();
  }
  private sealed class EditProbe : Pages.Edit
  {
    public EditProbe(ISubdivisionService service, DialogService dialogs, NotificationService notifications) {
      SubdivisionService = service; DialogService = dialogs; NotificationService = notifications; Logger = NullLogger<Pages.Edit>.Instance;
    }
    public bool Loaded => isLoaded;
    public Task Initialize() => OnInitializedAsync();
  }
  private sealed class MoveProbe : Pages.Move
  {
    public MoveProbe(ISubdivisionService service, DialogService dialogs, NotificationService notifications) {
      SubdivisionService = service; DialogService = dialogs; NotificationService = notifications; Logger = NullLogger<Pages.Move>.Instance;
    }
    public bool Loaded => isLoaded;
    public Task Initialize() => OnInitializedAsync();
  }
  private sealed class Navigation : NavigationManager
  {
    public Navigation() => Initialize("https://guard.test/", "https://guard.test/");
    protected override void NavigateToCore(string uri, bool forceLoad) { }
  }
  private sealed class NoJs : IJSRuntime
  {
    public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!);
    public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(T)!);
  }
}
