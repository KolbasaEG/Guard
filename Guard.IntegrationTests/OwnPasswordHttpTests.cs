using System.Net;
using System.Text.RegularExpressions;
using Guard.Core.Contexts;
using Microsoft.EntityFrameworkCore;

internal static class OwnPasswordHttpTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory)
  {
    var cookies = new CookieContainer();
    var checks = 0;
    using var handler = new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false };
    using var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5079") };
    client.DefaultRequestHeaders.Add("X-Forwarded-For", "198.18.11.1");
    void Check(bool value, string scenario) {
      if (!value) throw new InvalidOperationException($"Own password HTTP: {scenario}");
      checks++;
    }
    string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
      "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    var login = await client.GetStringAsync("/Account/Login");
    var token = Token(login);
    Check(token.Length > 0, "login antiforgery token available");
    var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> {
      ["__RequestVerificationToken"] = token, ["_handler"] = "login",
      ["Input.Email"] = "root@browser.test", ["Input.Password"] = "BrowserRoot123!"
    }));
    Check(signedIn.StatusCode == HttpStatusCode.Redirect, "login succeeds");
    var page = await client.GetStringAsync("/Account/Manage/ChangePassword");
    token = Token(page);
    Check(token.Length > 0, "authenticated antiforgery token available");
    Check(!page.Contains("Manage your account"), "mandatory page uses AccountLayout");
    page = await client.GetStringAsync("/Account/Manage/ChangePassword?ReturnUrl=%2FLogs%3Fview%3Derrors");
    Check(page.Contains("href=\"/Logs?view=errors\"") && !WebUtility.HtmlDecode(page).Contains("Обязательная смена пароля"),
      "ordinary password page returns to local source including query");
    page = await client.GetStringAsync("/Account/Manage/ChangePassword?ReturnUrl=https%3A%2F%2Fexample.test");
    Check(page.Contains("href=\"/\""), "ordinary password cancellation rejects external ReturnUrl");
    using (var denied = await client.PostAsync("/Account/Manage/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string> {
      ["_handler"] = "change-password", ["Input.OldPassword"] = "BrowserRoot123!", ["Input.NewPassword"] = "BrowserRoot456!", ["Input.ConfirmPassword"] = "BrowserRoot456!"
    }))) Check(denied.StatusCode == HttpStatusCode.BadRequest, "SSR password form rejects missing antiforgery");
    var oldCookies = cookies.GetCookieHeader(client.BaseAddress!);
    async Task<HttpResponseMessage> Change(string old, string password, string confirmation, bool csrf = true) {
      var values = new Dictionary<string, string> {
        ["OldPassword"] = old, ["NewPassword"] = password, ["Confirmation"] = confirmation
      };
      if (csrf) values["__RequestVerificationToken"] = token;
      return await client.PostAsync("/Account/ChangeOwnPassword", new FormUrlEncodedContent(values));
    }
    using (var denied = await Change("BrowserRoot123!", "BrowserRoot456!", "BrowserRoot456!", false))
      Check(!denied.IsSuccessStatusCode, "missing antiforgery token rejected");
    using (var mismatch = await Change("BrowserRoot123!", "BrowserRoot456!", "different"))
      Check(mismatch.StatusCode == HttpStatusCode.BadRequest, "confirmation checked on server");
    using (var empty = await Change("", "BrowserRoot456!", "BrowserRoot456!"))
      Check(empty.StatusCode == HttpStatusCode.BadRequest, "empty current password rejected on server");
    using (var wrong = await Change("wrong", "BrowserRoot456!", "BrowserRoot456!"))
      Check(wrong.StatusCode == HttpStatusCode.BadRequest, "incorrect current password rejected");
    using (var weak = await Change("BrowserRoot123!", "short", "short"))
      Check(weak.StatusCode == HttpStatusCode.BadRequest, "password policy checked on server");
    using (var changed = await Change("BrowserRoot123!", "BrowserRoot456!", "BrowserRoot456!"))
      Check(changed.IsSuccessStatusCode, "self change succeeds");
    using (var current = await client.GetAsync("/Account/Manage/ChangePassword"))
      Check(current.IsSuccessStatusCode, "refreshed cookie retains access");
    using (var staleHandler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
    using (var staleClient = new HttpClient(staleHandler) { BaseAddress = client.BaseAddress }) {
      staleClient.DefaultRequestHeaders.Add("Cookie", oldCookies);
      using var stale = await staleClient.GetAsync("/Account/Manage/ChangePassword");
      Check(stale.StatusCode == HttpStatusCode.Redirect && stale.Headers.Location?.ToString() == "/Account/Login",
        "previous cookie revoked");
    }
    page = await client.GetStringAsync("/Account/Manage/ChangePassword");
    token = Token(page);
    using (var restored = await Change("BrowserRoot456!", "BrowserRoot123!", "BrowserRoot123!"))
      Check(restored.IsSuccessStatusCode, "browser fixture password restored");
    async Task<HttpResponseMessage> ChangeFromPage(string password, string confirmation, string returnUrl = "/", string old = "BrowserRoot123!") {
      var html = await client.GetStringAsync("/Account/Manage/ChangePassword");
      return await client.PostAsync("/Account/Manage/ChangePassword?ReturnUrl=" + Uri.EscapeDataString(returnUrl), new FormUrlEncodedContent(new Dictionary<string, string> {
        ["__RequestVerificationToken"] = Token(html), ["_handler"] = "change-password",
        ["Input.OldPassword"] = old, ["Input.NewPassword"] = password,
        ["Input.ConfirmPassword"] = confirmation
      }));
    }
    using (var invalid = await ChangeFromPage("BrowserRoot789!", "different"))
      Check(invalid.StatusCode == HttpStatusCode.OK && WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync()).Contains("Пароли не совпадают"),
        "SSR form displays confirmation error without redirecting to success");
    using (var changed = await ChangeFromPage("BrowserRoot789!", "BrowserRoot789!", "/Logs?view=errors"))
      Check(changed.StatusCode == HttpStatusCode.Redirect && changed.Headers.Location != null &&
        new Uri(client.BaseAddress!, changed.Headers.Location).PathAndQuery == "/Logs?view=errors",
        $"SSR form changes own password through shared adapter (status {changed.StatusCode}, redirect {changed.Headers.Location})");
    page = await client.GetStringAsync("/Account/Manage/ChangePassword");
    token = Token(page);
    using (var restored = await Change("BrowserRoot789!", "BrowserRoot123!", "BrowserRoot123!"))
      Check(restored.IsSuccessStatusCode, "SSR success refreshes cookie and persisted password works through HTTP endpoint");
    await using (var db = await factory.CreateDbContextAsync()) {
      await db.Users.Where(u => u.Id == "root").ExecuteUpdateAsync(s => s.SetProperty(u => u.MustChangePassword, true));
      using (var changed = await ChangeFromPage("BrowserRootMandatory123!", "BrowserRootMandatory123!", "/Logs"))
        Check(changed.StatusCode == HttpStatusCode.Redirect && new Uri(client.BaseAddress!, changed.Headers.Location!).AbsolutePath == "/",
          "mandatory password success ignores source and opens home");
      Check(!await db.Users.Where(u => u.Id == "root").Select(u => u.MustChangePassword).SingleAsync(), "mandatory save clears server restriction");
      page = await client.GetStringAsync("/Account/Manage/ChangePassword"); token = Token(page);
      using (var restored = await Change("BrowserRootMandatory123!", "BrowserRoot123!", "BrowserRoot123!"))
        Check(restored.IsSuccessStatusCode, "mandatory change refreshes session and fixture password can be restored");
      await db.Users.Where(u => u.Id == "root").ExecuteUpdateAsync(s => s.SetProperty(u => u.MustChangePassword, true));
      page = await client.GetStringAsync("/Account/Manage/ChangePassword?mandatory=false&ReturnUrl=%2FLogs");
      Check(page.Contains("/Account/Logout?ReturnUrl=") && WebUtility.HtmlDecode(page).Contains("Обязательная смена пароля") &&
        !WebUtility.HtmlDecode(page).Contains(">Отмена</a>"), "mandatory page exposes logout confirmation without cancel");
      using (var restricted = await client.GetAsync("/"))
        Check(restricted.StatusCode == HttpStatusCode.Redirect && restricted.Headers.Location?.ToString() == "/Account/Manage/ChangePassword",
          "mandatory policy still restricts other pages");
      page = await client.GetStringAsync("/Account/Logout?ReturnUrl=%2FAccount%2FManage%2FChangePassword");
      Check(page.Contains("action=\"Account/Logout\"") && page.Contains("href=\"/Account/Manage/ChangePassword\""),
        "mandatory logout GET has protected POST and safe cancellation link");
      using var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> {
        ["__RequestVerificationToken"] = Token(page), ["returnUrl"] = "/Account/Login"
      }));
      Check(logout.StatusCode == HttpStatusCode.Redirect && logout.Headers.Location?.ToString() == "/Account/Login",
        "mandatory password change permits logout");
      using var denied = await client.GetAsync("/Account/Manage/ChangePassword");
      Check(denied.StatusCode == HttpStatusCode.Redirect, "logout clears authentication cookie");
      using var anonymous = await Change("BrowserRoot123!", "BrowserRoot456!", "BrowserRoot456!");
      Check(!anonymous.IsSuccessStatusCode, "anonymous own password POST cannot change password");
      await db.Users.Where(u => u.Id == "root").ExecuteUpdateAsync(s => s.SetProperty(u => u.MustChangePassword, false));
    }
    Console.WriteLine($"Passed {checks} own-password/logout HTTP checks on isolated database.");
  }
}
