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
    await using (var db = await factory.CreateDbContextAsync()) {
      db.IsSynchronization = true; // Только подготовка синтетических данных изолированного стенда.
      var subdivisions = Enumerable.Range(1, 27).Select(n => new Subdivision {
        Name = n == 26 ? "Архив отдела" : n == 27 ? "Удалённый отдел" : $"Поиск отдела {n:00}",
        Status = n == 26 ? Guard.Core.Enums.Status.Archived : n == 27 ? Guard.Core.Enums.Status.Deleted : Guard.Core.Enums.Status.Inserted,
        StaffCount = n * 1.5, LevelOrder = n, IsDepartment = n % 2 == 0, CreatedBy = "root"
      }).ToArray();
      db.AddRange(subdivisions); await db.SaveChangesAsync();
      db.Classifiers.AddRange(Enumerable.Range(1, 25).Select(n => new Classifier {
        Type = 998812, Code = n, ClassifierName = "Проверка DTO", Value = $"Значение {n:00}", IsActive = n % 2 == 0
      }));
      db.Users.AddRange(Enumerable.Range(1, 25).Select(n => new ApplicationUser {
        UserName = $"dto-user-{n:00}", NormalizedUserName = $"DTO-USER-{n:00}",
        Email = $"dto-{n:00}@browser.test", NormalizedEmail = $"DTO-{n:00}@BROWSER.TEST",
        SecurityStamp = Guid.NewGuid().ToString(), ConcurrencyStamp = Guid.NewGuid().ToString(),
        AccountBlockReason = n % 2 == 0 ? "Administrator" : null
      }));
      foreach (var subdivision in subdivisions) subdivision.Path = $"/{subdivision.SubdivisionId}/";
      db.IpAddresses.AddRange(Enumerable.Range(1, 25).Select(n => new IpAddress {
        Address = $"203.0.113.{n}", Description = $"Проверка пагинации {n:00}", CreatedBy = "root"
      }));
      db.IpAddresses.AddRange(
        new IpAddress { Address = "203.0.113.101", Description = "Архивная запись", Status = Guard.Core.Enums.Status.Archived, CreatedBy = "root" },
        new IpAddress { Address = "203.0.113.102", Description = "Удалённая запись", Status = Guard.Core.Enums.Status.Deleted, CreatedBy = "root" });
      await db.SaveChangesAsync();
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
    var exportDirectory = Path.Combine(Path.GetTempPath(), "guard-browser-export-" + Guid.NewGuid().ToString("N"));
    start.Environment["Exports__Directory"] = exportDirectory;
    start.Environment["DataProtection__KeyDirectory"] = Path.Combine(exportDirectory, "keys");
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
      if (Directory.Exists(exportDirectory)) Directory.Delete(exportDirectory, true);
    }
  }
}
