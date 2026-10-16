using System.Reflection;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Radzen;
using Pages = Guard.Components.Pages.Users;

internal static class UserDialogLifecycleTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var service = DispatchProxy.Create<IUserAdministrationService, IpAddressReferenceTests.ServiceProxy>();
    var proxy = (IpAddressReferenceTests.ServiceProxy)(object)service;
    using var dialogs = new DialogService(new Navigation(), new NoJs());
    var closes = 0; dialogs.OnClose += _ => closes++;
    var person = Guid.NewGuid();
    proxy.Handle = (_, _) => Task.FromResult<IReadOnlyList<UserPersonalOption>>([new(person, "Сотрудник")]);
    using var add = new Pages.Add(); Inject(add, service, dialogs, NullLogger<Pages.Add>.Instance);
    await Call(add, "OnInitializedAsync");
    var model = Get<CreateUserDto>(add, "model");
    model.UserName = "before"; model.Email = "user@example.test"; model.PersonalId = person; model.Password = "Secret123!";
    Set(add, "confirmation", model.Password);
    var saving = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    CreateUserCommand? command = null; CancellationToken token = default; var calls = 0;
    proxy.Handle = (_, args) => { calls++; command = (CreateUserCommand)args[0]!; token = (CancellationToken)args[1]!; return saving.Task; };
    var dialog = dialogs.OpenAsync<Pages.Add>("");
    var pending = Call(add, "SaveAsync"); await Call(add, "SaveAsync"); model.UserName = "after";
    check(calls == 1 && command!.UserName == "before", "user create blocks duplicate submit and sends immutable command");
    Cancel(add); await dialog;
    var next = dialogs.OpenAsync<Pages.Add>("");
    saving.SetResult("created"); await pending;
    check(token.IsCancellationRequested && closes == 1 && model.Password == "", "late user creation cannot close next dialog and cancel clears password");
    dialogs.Close(null); await next; closes = 0;

    var loading = new TaskCompletionSource<UserEditData>(TaskCreationOptions.RunContinuationsAsynchronously);
    proxy.Handle = (_, args) => { token = (CancellationToken)args[1]!; return loading.Task; };
    var edit = new Pages.Edit(); Inject(edit, service, dialogs, NullLogger<Pages.Edit>.Instance);
    var initializing = Call(edit, "OnInitializedAsync"); edit.Dispose(); edit.Dispose();
    loading.SetResult(new(new(), [], true)); await initializing;
    check(!Get<bool>(edit, "loaded") && token.IsCancellationRequested, "disposed user editor ignores late details and supports repeated dispose");

    proxy.Handle = (_, _) => Task.FromResult(new UserEditData(new() { Id = "user", Version = "v1", UserName = "before", Email = "a@example.test" }, [], true));
    using var activeEdit = new Pages.Edit(); Inject(activeEdit, service, dialogs, NullLogger<Pages.Edit>.Instance);
    await Call(activeEdit, "OnInitializedAsync");
    var updating = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    EditUserDto? snapshot = null;
    proxy.Handle = (_, args) => { snapshot = (EditUserDto)args[0]!; token = (CancellationToken)args[1]!; return updating.Task; };
    dialog = dialogs.OpenAsync<Pages.Edit>(""); pending = Call(activeEdit, "SaveAsync");
    Get<EditUserDto>(activeEdit, "model").UserName = "after";
    check(snapshot!.UserName == "before", "user editor submits independent field snapshot with version");
    Cancel(activeEdit); await dialog; next = dialogs.OpenAsync<Pages.Edit>("");
    updating.SetException(new IOException("late failure")); await pending;
    check(token.IsCancellationRequested && closes == 1 && Get<string?>(activeEdit, "error") == null, "late user edit error does not affect next dialog");
    dialogs.Close(null); await next; closes = 0;

    using var reset = new Pages.ResetPassword(); Inject(reset, service, dialogs, NullLogger<Pages.ResetPassword>.Instance);
    typeof(Pages.ResetPassword).GetProperty("UserId")!.SetValue(reset, "user");
    Get<CreateUserDto>(reset, "model").Password = "Temporary123!"; Set(reset, "confirmation", "Temporary123!");
    var resetting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    proxy.Handle = (_, args) => { token = (CancellationToken)args[1]!; return resetting.Task; };
    dialog = dialogs.OpenAsync<Pages.ResetPassword>(""); pending = Call(reset, "SaveAsync");
    Cancel(reset); await dialog; next = dialogs.OpenAsync<Pages.Edit>(""); resetting.SetResult(); await pending;
    check(token.IsCancellationRequested && closes == 1 && Get<CreateUserDto>(reset, "model").Password == "", "late password reset cannot close following dialog and clears secret");
    dialogs.Close(null); await next;
  }
  private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
  private static void Inject<T>(T page, IUserAdministrationService service, DialogService dialogs, Microsoft.Extensions.Logging.ILogger<T> logger) {
    page!.GetType().GetProperty("Security", Flags)!.SetValue(page, service);
    (page.GetType().GetProperty("Dialogs", Flags) ?? page.GetType().GetProperty("DialogService", Flags))!.SetValue(page, dialogs);
    page.GetType().GetProperty("Logger", Flags)!.SetValue(page, logger);
  }
  private static T Get<T>(object page, string name) => (T)page.GetType().GetField(name, Flags)!.GetValue(page)!;
  private static void Set(object page, string name, object value) => page.GetType().GetField(name, Flags)!.SetValue(page, value);
  private static Task Call(object page, string name) => (Task)page.GetType().GetMethod(name, Flags)!.Invoke(page, null)!;
  private static void Cancel(object page) => page.GetType().GetMethod("Cancel", Flags)!.Invoke(page, null);
  private sealed class Navigation : NavigationManager
  {
    public Navigation() => Initialize("https://guard.test/", "https://guard.test/");
    protected override void NavigateToCore(string uri, bool forceLoad) { }
  }
  private sealed class NoJs : IJSRuntime
  {
    public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!);
    public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args) => ValueTask.FromResult(default(T)!);
  }
}
