using System.Diagnostics;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class BrowserTestHost
{
  public static async Task RunAsync(IDbContextFactory<ApplicationDbContext> factory, string isolatedConnection)
  {
    await using (var db = await factory.CreateDbContextAsync()) {
      // Development seeder пропускает справочники, если тестовые данные уже есть.
      if (!await db.OrganTypes.AnyAsync()) {
        var classifier = new Classifier { Type = 906, Code = 99991, Value = "Browser test", ClassifierName = "Browser test" };
        db.Add(classifier);
        db.Add(new OrganType { Id = 99991, Code = classifier.Code, Name = "Browser test", Classifier = classifier });
        await db.SaveChangesAsync();
      }
    }
    var services = new ServiceCollection().AddLogging();
    services.AddScoped(_ => factory.CreateDbContext());
    services.AddIdentity<ApplicationUser, ApplicationRole>().AddEntityFrameworkStores<ApplicationDbContext>();
    await using var provider = services.BuildServiceProvider();
    await using (var scope = provider.CreateAsyncScope()) {
      var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
      var root = (await users.FindByIdAsync("root"))!;
      root.Email = "root@browser.test";
      if (!(await users.UpdateAsync(root)).Succeeded || !(await users.AddPasswordAsync(root, "BrowserRoot123!")).Succeeded)
        throw new InvalidOperationException("Не удалось подготовить тестовый Root.");
    }
    var start = new ProcessStartInfo("dotnet") {
      UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
    };
    var testAssembly = Environment.GetEnvironmentVariable("GUARD_TEST_APP_DLL");
    if (string.IsNullOrEmpty(testAssembly)) {
      foreach (var argument in new[] { "run", "--project", "Guard/Guard.csproj", "--no-build", "--no-launch-profile", "--urls", "http://127.0.0.1:5079" }) start.ArgumentList.Add(argument);
    } else {
      start.ArgumentList.Add(Path.GetFullPath(testAssembly));
      start.ArgumentList.Add("--urls"); start.ArgumentList.Add("http://127.0.0.1:5079");
      start.WorkingDirectory = Path.GetFullPath("Guard");
    }
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    start.Environment["ASPNETCORE_STATICWEBASSETS"] = string.IsNullOrEmpty(testAssembly)
      ? Path.GetFullPath("Guard/bin/Debug/net10.0/Guard.staticwebassets.runtime.json")
      : Path.ChangeExtension(Path.GetFullPath(testAssembly), "staticwebassets.runtime.json");
    start.Environment["ConnectionStrings__DefaultConnection"] = isolatedConnection;
    start.Environment["ConnectionStrings__LogsConnection"] = isolatedConnection;
    start.Environment["Audit__Syslog__Host"] = "127.0.0.1";
    start.Environment["Audit__Syslog__Port"] = "9";
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Не удалось запустить тестовый сервер.");
    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();
    try {
      using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
      var ready = false;
      for (var i = 0; i < 30 && !process.HasExited; i++) {
        try { ready = (await client.GetAsync("http://127.0.0.1:5079/Account/Login")).IsSuccessStatusCode; if (ready) break; }
        catch (HttpRequestException) { }
        catch (TaskCanceledException) { }
        await Task.Delay(500);
      }
      if (!ready) throw new InvalidOperationException("Тестовый сервер не готов. См. Guard/obj/account-policy-browser.log.");
      await OwnPasswordHttpTests.RunAsync(factory);
      Console.WriteLine("Isolated browser server: http://127.0.0.1:5079 ; Root: root@browser.test / BrowserRoot123! ; press Enter to stop and remove temporary DB.");
      await Console.In.ReadLineAsync();
    }
    finally {
      if (!process.HasExited) process.Kill(entireProcessTree: true);
      await process.WaitForExitAsync();
      await File.WriteAllTextAsync("Guard/obj/account-policy-browser.log", await output + await error);
    }
  }
}
