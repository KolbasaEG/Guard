using System.Net;
using System.Text.RegularExpressions;
using Guard.Core.Contexts;
using Microsoft.EntityFrameworkCore;

internal static class OwnPasswordHttpTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory)
  {
    var cookies = new CookieContainer();
    using var handler = new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false };
    using var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5079") };
    void Check(bool value, string scenario) {
      if (!value) throw new InvalidOperationException($"Own password HTTP: {scenario}");
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
    await using (var db = await factory.CreateDbContextAsync()) {
      await db.Users.Where(u => u.Id == "root").ExecuteUpdateAsync(s => s.SetProperty(u => u.MustChangePassword, true));
      page = await client.GetStringAsync("/Account/Manage/ChangePassword");
      Check(page.Contains("action=\"Account/Logout\"") && WebUtility.HtmlDecode(page).Contains("Сохранить пароль"),
        "mandatory page exposes separate logout POST and explicit save button");
      using var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> {
        ["__RequestVerificationToken"] = Token(page), ["returnUrl"] = "/Account/Login"
      }));
      Check(logout.StatusCode == HttpStatusCode.Redirect && logout.Headers.Location?.ToString() == "/Account/Login",
        "mandatory password change permits logout");
      using var denied = await client.GetAsync("/Account/Manage/ChangePassword");
      Check(denied.StatusCode == HttpStatusCode.Redirect, "logout clears authentication cookie");
      await db.Users.Where(u => u.Id == "root").ExecuteUpdateAsync(s => s.SetProperty(u => u.MustChangePassword, false));
    }
    Console.WriteLine("Passed 15 own-password/logout HTTP checks on isolated database.");
  }
}
