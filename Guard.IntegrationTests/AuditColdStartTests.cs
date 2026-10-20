using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

internal static class AuditColdStartTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var directory = Path.Combine(Path.GetTempPath(), "guard-audit-cold-start-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
    // Порт принадлежит этому тесту, но PostgreSQL на нём нет: чужая БД не может быть создана или изменена.
    using var unavailableEndpoint = new TcpListener(IPAddress.Loopback, 0); unavailableEndpoint.Start();
    var unavailablePort = ((IPEndPoint)unavailableEndpoint.LocalEndpoint).Port;
    var unavailable = $"Host=127.0.0.1;Port={unavailablePort};Database=guard_unreachable_fixture;Username=fixture;Timeout=1;Command Timeout=1;Pooling=false";
    var assembly = Path.GetFullPath(Environment.GetEnvironmentVariable("GUARD_TEST_APP_DLL") ?? "Guard/bin/Debug/net10.0/Guard.dll");
    var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
      RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = directory };
    start.ArgumentList.Add(assembly); start.ArgumentList.Add("--urls"); start.ArgumentList.Add($"http://127.0.0.1:{port}");
    start.ArgumentList.Add("--contentRoot"); start.ArgumentList.Add(Path.GetFullPath("Guard"));
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
    start.Environment["ConnectionStrings__DefaultConnection"] = unavailable;
    start.Environment["ConnectionStrings__LogsConnection"] = unavailable;
    start.Environment["DataProtection__KeyDirectory"] = Path.Combine(directory, "keys");
    start.Environment["Exports__Directory"] = Path.Combine(directory, "exports");
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start isolated failure host");
    var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
    try {
      using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) {
        BaseAddress = new($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(3)
      };
      string? refusal = null;
      using var ready = new CancellationTokenSource(TimeSpan.FromSeconds(25));
      while (refusal == null && !process.HasExited) {
        ready.Token.ThrowIfCancellationRequested();
        try { using var response = await client.GetAsync("/Account/AuditUnavailable", ready.Token);
          if (response.IsSuccessStatusCode) refusal = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(ready.Token));
        } catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { }
        if (refusal == null) await Task.Delay(150, ready.Token);
      }
      check(refusal?.Contains("Работа приостановлена") == true && refusal.Contains("__RequestVerificationToken"),
        "cold start without Identity/audit DB serves safe refusal and logout form");
      using var diagnostic = await client.GetAsync("/audit/settings");
      check(diagnostic.StatusCode == HttpStatusCode.Redirect && diagnostic.Headers.Location?.ToString().Contains("Account/Login") == true,
        "unavailable database cannot grant anonymous Root diagnostics at cold start");
    }
    finally {
      if (!process.HasExited) process.Kill(entireProcessTree: true);
      await process.WaitForExitAsync(); await output; await error;
      var full = Path.GetFullPath(directory);
      if (!Path.GetFileName(full).StartsWith("guard-audit-cold-start-", StringComparison.Ordinal) || Path.GetDirectoryName(full) != Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar))
        throw new InvalidOperationException("Unexpected temporary test directory");
      Directory.Delete(full, true);
    }
  }
}
