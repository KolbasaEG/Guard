using System.Net;
using System.Text.RegularExpressions;
using Guard.Core.Contexts;
using Microsoft.EntityFrameworkCore;

internal static class LocalAccountHttpTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory)
  {
    var checks = 0;
    using var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false };
    using var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5079") };
    void Check(bool value, string scenario) {
      if (!value) throw new InvalidOperationException($"Local account HTTP: {scenario}");
      checks++;
    }
    string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
      "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    async Task<HttpResponseMessage> Login(string password, string returnUrl = "/Logs", bool csrf = true) {
      var html = await client.GetStringAsync("/Account/Login");
      var values = new Dictionary<string, string> {
        ["_handler"] = "login", ["Input.Email"] = "ROOT@browser.test", ["Input.Password"] = password
      };
      if (csrf) values["__RequestVerificationToken"] = Token(html);
      return await client.PostAsync("/Account/Login?ReturnUrl=" + Uri.EscapeDataString(returnUrl), new FormUrlEncodedContent(values));
    }
    using (var denied = await Login("BrowserRoot123!", csrf: false))
      Check(denied.StatusCode == HttpStatusCode.BadRequest, "login requires antiforgery");
    using (var invalid = await Login("wrong"))
      Check(invalid.StatusCode == HttpStatusCode.OK && WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync()).Contains("Неверный email или пароль") &&
        (await invalid.Content.ReadAsStringAsync()).Contains("alert-danger"),
        "wrong password displays generic error");
    using (var login = await Login("BrowserRoot123!"))
      Check(login.StatusCode == HttpStatusCode.Redirect && new Uri(client.BaseAddress!, login.Headers.Location!).AbsolutePath == "/Logs",
        "login accepts email distinct from username and local ReturnUrl");
    foreach (var path in new[] {
      "Register", "RegisterConfirmation", "ConfirmEmail", "ConfirmEmailChange", "ForgotPassword", "ForgotPasswordConfirmation",
      "ResetPassword", "ResetPasswordConfirmation", "ResendEmailConfirmation", "InvalidPasswordReset", "ExternalLogin",
      "LoginWith2fa", "LoginWithRecoveryCode", "Manage", "Manage/Email", "Manage/PersonalData", "Manage/DeletePersonalData",
      "Manage/SetPassword", "Manage/ExternalLogins", "Manage/Passkeys", "Manage/RenamePasskey/test", "Manage/TwoFactorAuthentication",
      "Manage/EnableAuthenticator", "Manage/Disable2fa", "Manage/ResetAuthenticator", "Manage/GenerateRecoveryCodes"
    }) {
      using var get = await client.GetAsync("/Account/" + path);
      Check(get.StatusCode == HttpStatusCode.NotFound, "removed GET route is closed: " + path);
      using var post = await client.PostAsync("/Account/" + path, new FormUrlEncodedContent(new Dictionary<string, string>()));
      Check(post.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, "removed POST route is closed: " + path);
    }
    foreach (var path in new[] { "PerformExternalLogin", "PasskeyCreationOptions", "PasskeyRequestOptions", "Manage/LinkExternalLogin", "Manage/DownloadPersonalData" }) {
      using var post = await client.PostAsync("/Account/" + path, new FormUrlEncodedContent(new Dictionary<string, string>()));
      Check(post.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, "removed endpoint is closed: " + path);
    }
    var page = await client.GetStringAsync("/Account/Logout");
    Check(page.Contains("__RequestVerificationToken"), "logout GET renders confirmation");
    using (var stillSignedIn = await client.GetAsync("/Account/Manage/ChangePassword"))
      Check(stillSignedIn.IsSuccessStatusCode, "logout GET does not clear authentication");
    using (var denied = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> { ["returnUrl"] = "/" })))
      Check(denied.StatusCode == HttpStatusCode.BadRequest, "logout rejects missing antiforgery");
    using (var stillSignedIn = await client.GetAsync("/Account/Manage/ChangePassword"))
      Check(stillSignedIn.IsSuccessStatusCode, "failed logout leaves cookie usable");
    using (var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> {
      ["__RequestVerificationToken"] = Token(page), ["returnUrl"] = "https://example.test"
    })))
      Check(logout.StatusCode == HttpStatusCode.Redirect && logout.Headers.Location?.ToString() == "/", "logout rejects external ReturnUrl");
    await using var db = await factory.CreateDbContextAsync();
    var originalHash = await db.Users.Where(u => u.Id == "root").Select(u => u.PasswordHash).SingleAsync();
    try {
      await db.Users.Where(u => u.Id == "root").ExecuteUpdateAsync(s => s.SetProperty(u => u.TwoFactorEnabled, true));
      using (var denied = await Login("BrowserRoot123!"))
        Check(denied.StatusCode == HttpStatusCode.OK && WebUtility.HtmlDecode(await denied.Content.ReadAsStringAsync()).Contains("включена двухфакторная проверка"),
          "2FA account receives administrator message");
      using (var denied = await client.GetAsync("/Account/Manage/ChangePassword"))
        Check(denied.StatusCode == HttpStatusCode.Redirect, "2FA password check does not issue application cookie");
      Check(await db.Users.Where(u => u.Id == "root").Select(u => u.TwoFactorEnabled).SingleAsync(), "2FA flag preserved");
      await db.Users.Where(u => u.Id == "root").ExecuteUpdateAsync(s => s.SetProperty(u => u.TwoFactorEnabled, false));
      using (var login = await Login("BrowserRoot123!", "//example.test"))
        Check(login.StatusCode == HttpStatusCode.Redirect && new Uri(client.BaseAddress!, login.Headers.Location!).AbsolutePath == "/", "login rejects external ReturnUrl");
      await db.Users.Where(u => u.Id == "root").ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, (string?)null));
      page = await client.GetStringAsync("/Account/Manage/ChangePassword");
      Check(WebUtility.HtmlDecode(page).Contains("не установлен локальный пароль") && !page.Contains("change-password"),
        "account without password sees administrator message rather than SetPassword form");
    }
    finally {
      await db.Users.Where(u => u.Id == "root").ExecuteUpdateAsync(s => s.SetProperty(u => u.TwoFactorEnabled, false).SetProperty(u => u.PasswordHash, originalHash));
    }
    Console.WriteLine($"Passed {checks} local-account HTTP checks on isolated database.");
  }
}
