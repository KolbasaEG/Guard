// Проверка обработчика отправки без renderer; параметры задаются тестовым стендом.
#pragma warning disable BL0005
using Guard.Core.Services;
using Guard.Core.Identity;
using Microsoft.Extensions.Logging.Abstractions;

internal static class AccessEventTests
{
  public static async Task RunAsync(Action<bool,string> check)
  {
    var notifier = new AccessChangeNotifier(NullLogger<AccessChangeNotifier>.Instance);
    var first = 0; var second = 0; var other = 0;
    using var a = notifier.Subscribe("user", () => { first++; return Task.CompletedTask; });
    using var b = notifier.Subscribe("user", () => { second++; return Task.CompletedTask; });
    using var c = notifier.Subscribe("other", () => { other++; return Task.CompletedTask; });
    await notifier.PublishAsync(["user", "user"]);
    check(first == 1 && second == 1 && other == 0, "all sessions notified once, other users isolated");
    b.Dispose();
    await notifier.PublishAsync(["user"]);
    check(first == 2 && second == 1, "disposed session unsubscribed");
    using var broken = notifier.Subscribe("user", () => throw new InvalidOperationException("closed circuit"));
    await notifier.PublishAsync(["user"]);
    check(first == 3, "failed subscriber does not prevent delivery");
    var submitted = 0;
    var model = new object();
    var state = new Guard.Components.Library.Authorization.DialogAccessState(true, true, null, () => Task.CompletedTask);
    var form = new TestForm {
      Data = model, AccessState = state,
      Submit = Microsoft.AspNetCore.Components.EventCallback.Factory.Create(new object(), () => submitted++)
    };
    state.CanWrite = false;
    await form.SendAsync();
    check(submitted == 0 && ReferenceEquals(form.Data, model), "revoked write blocks submit without replacing form data");
    state.CanView = false; state.CanWrite = true;
    await form.SendAsync();
    check(submitted == 0, "revoked read blocks submit");
    state.CanView = true;
    await form.SendAsync();
    check(submitted == 1, "restored access permits existing form submission");
    form.AccessState = new(true, true, null, () => {
      state.CanWrite = false; form.AccessState = state; return Task.CompletedTask;
    });
    await form.SendAsync();
    check(submitted == 1, "pre-submit refresh detects missed revocation");
    var coordinator = new AccessRefreshCoordinator();
    var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    Func<Task> pending = () => complete.Task;
    Func<Task> ready = () => Task.CompletedTask;
    coordinator.RefreshRequested += pending;
    coordinator.RefreshRequested += ready;
    var refresh = coordinator.RequestAsync();
    check(!refresh.IsCompleted, "connection refresh awaits all subscribers");
    complete.SetResult();
    await refresh;
    coordinator.RefreshRequested -= pending;
    coordinator.RefreshRequested -= ready;
    await coordinator.RequestAsync();
    check(true, "refresh without active provider succeeds");
  }
  private sealed class TestForm : Guard.Components.Library.Forms.EntityForm<object> {
    public Task SendAsync() => FormSubmit();
  }
}
