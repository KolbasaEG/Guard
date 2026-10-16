using System.Reflection;
using Guard.Components.Account.Pages.Manage;
using Guard.Components.Account.Shared;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

internal static class OwnPasswordUiCompletionTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var page = new ChangePassword();
    var passwords = new Passwords();
    Set(page, "Passwords", passwords);
    Set(page, "HttpContext", new DefaultHttpContext());
    Set(page, "Logger", NullLogger<ChangePassword>.Instance);
    await Invoke(page, "OnInitializedAsync");
    var input = Get(page, "Input")!;
    foreach (var name in new[] { "OldPassword", "NewPassword", "ConfirmPassword" }) Set(input, name, "synthetic");
    passwords.Status = OwnPasswordChangeStatus.SignInRequired;
    await Invoke(page, "OnValidSubmitAsync");
    check((bool)Get(page, "signInRequired")! && !(bool)Get(page, "canChange")! &&
      (string)Get(page, "message")! == OwnPasswordChangeResult.SignInRequiredMessage,
      "SSR confirmed password with failed cookie hides form and offers sign-in message");
    input = Get(page, "Input")!;
    check(new[] { "OldPassword", "NewPassword", "ConfirmPassword" }.All(n => (string)Get(input, n)! == ""),
      "SSR confirmed password clears submitted secrets");
    passwords.Status = OwnPasswordChangeStatus.Rejected;
    var rejectedPage = new ChangePassword(); Set(rejectedPage, "Passwords", passwords); Set(rejectedPage, "HttpContext", new DefaultHttpContext());
    await Invoke(rejectedPage, "OnInitializedAsync");
    await Invoke(rejectedPage, "OnValidSubmitAsync");
    check(!(bool)Get(rejectedPage, "signInRequired")! && (bool)Get(rejectedPage, "canChange")!,
      "SSR rejected password keeps editable form without confirmed-change message");

    var js = new Js();
    await using var dialog = new ChangePasswordDialog();
    Set(dialog, "JS", js); Set(dialog, "Logger", NullLogger<ChangePasswordDialog>.Instance);
    var dialogInput = Get(dialog, "input")!;
    foreach (var name in new[] { "OldPassword", "NewPassword", "Confirmation" }) Set(dialogInput, name, "synthetic");
    await Invoke(dialog, "SaveAsync");
    check((bool)Get(dialog, "signInRequired")! && Get(dialog, "error") == null,
      "interactive dialog handles confirmed change as sign-in required, not rejection");
    check(new[] { "OldPassword", "NewPassword", "Confirmation" }.All(n => (string)Get(dialogInput, n)! == ""),
      "interactive confirmed password clears secrets");
    await Invoke(dialog, "SaveAsync");
    check(js.Calls == 1, "interactive sign-in-required state prevents password resubmission");
  }

  private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
  private static object? Get(object target, string name) => target.GetType().GetProperty(name, Flags)?.GetValue(target) ??
    target.GetType().GetField(name, Flags)?.GetValue(target);
  private static void Set(object target, string name, object value)
  {
    var property = target.GetType().GetProperty(name, Flags);
    if (property != null) property.SetValue(target, value); else target.GetType().GetField(name, Flags)!.SetValue(target, value);
  }
  private static Task Invoke(object target, string name) => (Task)target.GetType().GetMethod(name, Flags)!.Invoke(target, null)!;

  private sealed class Passwords : IOwnPasswordService
  {
    public OwnPasswordChangeStatus Status;
    public Task<OwnPasswordState> GetStateAsync(CancellationToken ct = default) => Task.FromResult(OwnPasswordState.Ready);
    public Task<OwnPasswordChangeResult> ChangeAsync(string currentPassword, string newPassword, string confirmation, CancellationToken ct = default) =>
      Task.FromResult(new OwnPasswordChangeResult(Status, "Synthetic rejection"));
  }
  private sealed class Js : IJSRuntime, IJSObjectReference
  {
    public int Calls;
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
      if (identifier == "import") return ValueTask.FromResult((TValue)(object)this);
      Calls++;
      return ValueTask.FromResult((TValue)(object)new ChangePasswordDialog.PasswordResponse(true, null));
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }
}
