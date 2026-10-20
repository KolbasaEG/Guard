using Guard.Components.Account;
using Guard.Core.Identity;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Http;

internal static class LocalAccountBoundaryTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var contexts = new HttpContextAccessor();
    var service = new LocalAccountService(contexts, null!, null!, new AuditStub());
    await PostgresTests.ThrowsAsync<InvalidOperationException>(() => service.SignInAsync("a", "b", false), check,
      "local login requires HTTP request");
    await PostgresTests.ThrowsAsync<InvalidOperationException>(() => service.SignOutAsync(), check,
      "local logout requires HTTP request");
    contexts.HttpContext = new DefaultHttpContext();
    check(await service.SignInAsync(" ", "password", false) == LocalSignInStatus.Failed &&
      await service.SignInAsync("a@b.test", "", false) == LocalSignInStatus.Failed,
      "local login validates empty credentials before Identity lookup");
    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
    await PostgresTests.ThrowsAsync<OperationCanceledException>(() => service.SignInAsync("a", "b", false, cancelled.Token), check,
      "cancelled login cannot issue cookie");
    await PostgresTests.ThrowsAsync<OperationCanceledException>(() => service.SignOutAsync(cancelled.Token), check,
      "cancelled logout cannot clear cookie");
    foreach (var unsafeUrl in new[] { "https://example.test", "//example.test", @"~/\/example.test", "/\\example.test", "javascript:alert(1)", "/\r\nLocation:evil" })
      check(AccountReturnUrl.Normalize(unsafeUrl) == "/", "external or ambiguous account return URL rejected");
    foreach (var (value, expected) in new[] { ("/ip?x=1", "/ip?x=1"), ("~/Logs", "/Logs"), ("Account/Login", "/Account/Login") })
      check(AccountReturnUrl.Normalize(value) == expected, "local account return URL preserved");
  }
}
