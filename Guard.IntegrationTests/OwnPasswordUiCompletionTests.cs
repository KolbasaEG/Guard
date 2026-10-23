using System.Reflection;
using System.Security.Claims;
using Guard.Components.Account.Pages.Manage;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

internal static class OwnPasswordUiCompletionTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var passwords = new Passwords { Status = OwnPasswordChangeStatus.SignInRequired };
    var policies = new Policies();
    ChangePassword Page()
    {
      var page = new ChangePassword();
      Set(page, "Passwords", passwords); Set(page, "Policies", policies);
      Set(page, "HttpContext", new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.NameIdentifier, "authenticated-user") }, "test")) });
      Set(page, "Logger", NullLogger<ChangePassword>.Instance);
      return page;
    }
    var page = Page();
    await Invoke(page, "OnInitializedAsync");
    var input = Get(page, "Input")!;
    foreach (var name in new[] { "OldPassword", "NewPassword", "ConfirmPassword" }) Set(input, name, "synthetic");
    await Invoke(page, "OnValidSubmitAsync");
    check((bool)Get(page, "signInRequired")! && !(bool)Get(page, "canChange")! &&
      (string)Get(page, "message")! == OwnPasswordChangeResult.SignInRequiredMessage,
      "SSR confirmed password with failed cookie hides form and offers sign-in message");
    check(new[] { "OldPassword", "NewPassword", "ConfirmPassword" }.All(n => (string)Get(input, n)! == ""),
      "SSR confirmed password clears submitted secrets");
    await Invoke(page, "OnValidSubmitAsync");
    check(passwords.Calls == 1, "SSR sign-in-required state prevents password resubmission");
    passwords.Status = OwnPasswordChangeStatus.Rejected;
    var rejectedPage = Page(); await Invoke(rejectedPage, "OnInitializedAsync");
    await Invoke(rejectedPage, "OnValidSubmitAsync");
    check(!(bool)Get(rejectedPage, "signInRequired")! && (bool)Get(rejectedPage, "canChange")! &&
      ((string)Get(rejectedPage, "message")!).Contains("Synthetic rejection"),
      "SSR rejected password retains editable form and rejection message");
    policies.Expired = true;
    var mandatoryPage = Page(); Set(mandatoryPage, "ReturnUrl", "/Logs");
    await Invoke(mandatoryPage, "OnInitializedAsync");
    check((bool)Get(mandatoryPage, "mandatory")! && policies.UserId == "authenticated-user",
      "mandatory state comes from policy of authenticated subject");
    policies.Expired = false;
    var ordinaryPage = Page(); await Invoke(ordinaryPage, "OnInitializedAsync");
    check(!(bool)Get(ordinaryPage, "mandatory")!, "ordinary state follows server policy");
    foreach (var url in new[] { "https://example.test", "//example.test", "/\\example.test", "\n/Logs" }) {
      Set(ordinaryPage, "ReturnUrl", url);
      check((string)Get(ordinaryPage, "SafeReturnUrl")! == "/", "password return rejects unsafe URL");
    }
    Set(ordinaryPage, "ReturnUrl", "/Logs?view=errors");
    check((string)Get(ordinaryPage, "SafeReturnUrl")! == "/Logs?view=errors", "password return preserves local query");
  }

  private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
  private static object? Get(object target, string name) => target.GetType().GetProperty(name, Flags)?.GetValue(target) ?? target.GetType().GetField(name, Flags)?.GetValue(target);
  private static void Set(object target, string name, object value) {
    var property = target.GetType().GetProperty(name, Flags);
    if (property != null) property.SetValue(target, value); else target.GetType().GetField(name, Flags)!.SetValue(target, value);
  }
  private static Task Invoke(object target, string name) => (Task)target.GetType().GetMethod(name, Flags)!.Invoke(target, null)!;
  private sealed class Passwords : IOwnPasswordService {
    public OwnPasswordChangeStatus Status;
    public int Calls;
    public Task<OwnPasswordState> GetStateAsync(CancellationToken ct = default) => Task.FromResult(OwnPasswordState.Ready);
    public Task<OwnPasswordChangeResult> ChangeAsync(string currentPassword, string newPassword, string confirmation, CancellationToken ct = default) {
      Calls++; return Task.FromResult(new OwnPasswordChangeResult(Status, "Synthetic rejection"));
    }
  }
  private sealed class Policies : IAccountPolicyService {
    public bool Expired;
    public string? UserId;
    public Task<AccountState> CheckAsync(string userId, CancellationToken ct = default) { UserId = userId; return Task.FromResult(new AccountState(false, Expired)); }
    public Task<AccountLoginPolicyDto> GetLoginPolicyAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<AccountPasswordLifetimeDto> GetPasswordLifetimeAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task RecordActivityAsync(string userId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task SweepAsync(CancellationToken ct = default) => throw new NotSupportedException();
  }
}
