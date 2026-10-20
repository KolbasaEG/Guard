using System.Net;
using System.Text.RegularExpressions;
using Guard.Core.Contexts;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;

internal static class AuditAvailabilityHttpTests
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory)
  {
    var checks = 0;
    void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException("Audit HTTP: " + name); checks++; }
    string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
      "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    HttpClient Client(string ip) {
      var result = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new() }) { BaseAddress = new("http://127.0.0.1:5079") };
      result.DefaultRequestHeaders.Add("X-Forwarded-For", ip); return result;
    }
    async Task<HttpResponseMessage> Login(HttpClient client, string path = "/Account/Login", string email = "root@browser.test", string password = "BrowserRoot123!") {
      var token = Token(await client.GetStringAsync("/Account/Login"));
      return await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string> {
        ["__RequestVerificationToken"] = token, ["_handler"] = "login", ["Input.Email"] = email, ["Input.Password"] = password
      }));
    }
    await using var db = await factory.CreateDbContextAsync();
    await db.AuditSettings.ExecuteUpdateAsync(s => s.SetProperty(c => c.Configured, false));
    try {
      using var initialRoot = Client("198.18.12.5");
      using (var login = await Login(initialRoot)) Check(login.StatusCode == HttpStatusCode.Redirect &&
        login.Headers.Location?.ToString().EndsWith("/Account/Audit") == true, "first startup allows Root to enter limited audit recovery");
      using (var settings = await initialRoot.GetAsync("/audit/settings")) Check(settings.IsSuccessStatusCode &&
        WebUtility.HtmlDecode(await settings.Content.ReadAsStringAsync()).Contains("NotConfigured"), "Root can open settings before selecting audit mode");
      using (var denied = await initialRoot.GetAsync("/personal")) Check(denied.StatusCode == HttpStatusCode.ServiceUnavailable,
        "first startup keeps ordinary operations blocked until Root configuration");
    } finally { await db.AuditSettings.ExecuteUpdateAsync(s => s.SetProperty(c => c.Configured, true)); }
    using var ordinary = Client("198.18.12.1");
    using (var malformed = await Login(ordinary, email: "invalid-email"))
      Check(malformed.StatusCode == HttpStatusCode.OK, "invalid login form is rejected before cookie issuance");
    using (var login = await Login(ordinary)) Check(login.StatusCode == HttpStatusCode.Redirect, "ordinary Root session starts while audit healthy");
    Check(await db.AuditEvents.AnyAsync(e => e.ReasonCode == "InvalidInput" && e.AttemptedAccount == "invalid-email"),
      "invalid login form retains its own safe rejection event");
    await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"AuditEvents\" RENAME TO \"AuditEventsOffline\"");
    try {
      using var blocked = await ordinary.GetAsync("/personal");
      Check(blocked.StatusCode == HttpStatusCode.ServiceUnavailable, "existing HTTP session cannot read during storage outage");
      using var recovery = Client("198.18.12.2");
      using (var login = await Login(recovery)) Check(login.StatusCode == HttpStatusCode.Redirect &&
        login.Headers.Location?.ToString().EndsWith("/Account/Audit") == true, "Root receives only recovery entry during audit outage");
      using (var diagnostic = await recovery.GetAsync("/audit/settings")) Check(diagnostic.IsSuccessStatusCode &&
        WebUtility.HtmlDecode(await diagnostic.Content.ReadAsStringAsync()).Contains("StorageUnavailable"), "Root can inspect actual storage failure in diagnostics");
      using (var denied = await recovery.GetAsync("/personal")) Check(denied.StatusCode == HttpStatusCode.ServiceUnavailable, "recovery cookie has no ordinary access");
      using (var denied = await recovery.PostAsync("/Account/ChangeOwnPassword", new FormUrlEncodedContent([])))
        Check(denied.StatusCode == HttpStatusCode.ServiceUnavailable, "recovery cookie cannot use own-password endpoint");
      var logoutPage = await recovery.GetStringAsync("/Account/Logout");
      using (var logout = await recovery.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string> {
        ["__RequestVerificationToken"] = Token(logoutPage), ["returnUrl"] = "/Account/Login"
      }))) Check(logout.StatusCode == HttpStatusCode.Redirect, "logout works while local journal is unavailable");

      using var retainedRecovery = Client("198.18.12.3");
      using (var login = await Login(retainedRecovery)) Check(login.StatusCode == HttpStatusCode.Redirect, "second limited recovery session created");
      await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"AuditEventsOffline\" RENAME TO \"AuditEvents\"");
      using (var denied = await retainedRecovery.GetAsync("/personal")) Check(denied.StatusCode == HttpStatusCode.ServiceUnavailable, "repair does not promote recovery cookie");
      using (var resumed = await ordinary.GetAsync("/personal")) Check(resumed.IsSuccessStatusCode, "repair automatically resumes existing ordinary session");
    }
    finally {
      if (await db.Database.SqlQueryRaw<bool>("SELECT to_regclass('public.\"AuditEventsOffline\"') IS NOT NULL AS \"Value\"").SingleAsync())
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"AuditEventsOffline\" RENAME TO \"AuditEvents\"");
    }
    using var limited = Client("198.18.12.4");
    for (var attempt = 0; attempt < 6; attempt++) {
      using var response = await Login(limited, attempt % 2 == 0 ? "/Account/Login" : "/account/login/", "missing@browser.test", "wrong");
      Check(attempt < 5 ? response.StatusCode != HttpStatusCode.TooManyRequests : response.StatusCode == HttpStatusCode.TooManyRequests,
        "login alias shares bounded rate limit, attempt " + attempt);
    }
    Check(await db.AuditEvents.AnyAsync(e => e.EventType == AuditEventType.LoginFailed && e.AttemptedAccount == "missing@browser.test" && e.ReasonCode == "UnknownAccount"),
      "failed login retains attempted name and machine-readable cause");
    Check(await db.AuditEvents.AnyAsync(e => e.EventType == AuditEventType.LoginFailed && e.ReasonCode == "RateLimited" && e.ClientIp == "198.18.12.4"),
      "HTTP rate rejection is audited without treating the request as an authenticated account");
    Console.WriteLine($"Passed {checks} audit outage/recovery HTTP checks on isolated database.");
  }
}
