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
      // Предыдущие интеграционные сценарии оставляют режим API; браузерный стенд начинает с ручного режима.
      await db.SynchronizationSettings.ExecuteUpdateAsync(s => s
        .SetProperty(x => x.Mode, DataManagementMode.Local).SetProperty(x => x.ScheduleEnabled, false));
      var subdivisions = Enumerable.Range(1, 27).Select(n => new Subdivision {
        Name = n == 26 ? "Архив отдела" : n == 27 ? "Удалённый отдел" : $"Поиск отдела {n:00}",
        Status = n == 26 ? Guard.Core.Enums.Status.Archived : n == 27 ? Guard.Core.Enums.Status.Deleted : Guard.Core.Enums.Status.Inserted,
        StaffCount = n * 1.5, LevelOrder = n, IsDepartment = n % 2 == 0, CreatedBy = "root"
      }).ToArray();
      db.AddRange(subdivisions); await db.SaveChangesAsync();
      db.Classifiers.AddRange(Enumerable.Range(1, 25).Select(n => new Classifier {
        Type = 998812, Code = n, ClassifierName = "Проверка DTO", Value = $"Значение {n:00}", IsActive = n % 2 == 0
      }));
      db.Roles.AddRange(Enumerable.Range(1, 25).Select(n => new ApplicationRole {
        Name = $"DTO роль {n:00}", NormalizedName = $"DTO РОЛЬ {n:00}", ConcurrencyStamp = Guid.NewGuid().ToString()
      }));
      db.Users.AddRange(Enumerable.Range(1, 25).Select(n => new ApplicationUser {
        UserName = $"dto-user-{n:00}", NormalizedUserName = $"DTO-USER-{n:00}",
        Email = $"dto-{n:00}@browser.test", NormalizedEmail = $"DTO-{n:00}@BROWSER.TEST",
        SecurityStamp = Guid.NewGuid().ToString(), ConcurrencyStamp = Guid.NewGuid().ToString(),
        AccountBlockReason = n % 2 == 0 ? "Administrator" : null
      }));
      foreach (var subdivision in subdivisions) subdivision.Path = $"/{subdivision.SubdivisionId}/";
      subdivisions[1].ParentId = subdivisions[0].Id;
      subdivisions[1].Path = subdivisions[0].Path + subdivisions[1].SubdivisionId + "/";
      var people = Enumerable.Range(1, 27).Select(n => new Personal {
        LastName = $"Проверка {n:00}", FirstName = "Браузер", MiddleName = "Тест",
        SubdivisionId = subdivisions[n % 3].Id, CreatedBy = "root",
        Status = n == 26 ? Guard.Core.Enums.Status.Archived : n == 27 ? Guard.Core.Enums.Status.Deleted : Guard.Core.Enums.Status.Inserted
      }).ToArray();
      db.Personals.AddRange(people);
      db.NotificationEvents.AddRange(Enumerable.Range(1, 27).Select(n => new NotificationEvent {
        EventKey = $"browser-notification-{n:00}", UserId = "root", Kind = Guard.Core.Enums.UserNotificationKind.SynchronizationResult,
        Severity = Guard.Core.Enums.UserNotificationSeverity.Information, Title = $"Проверка уведомления {n:00}",
        Message = $"Результат фоновой операции {n:00} для проверки истории.", Target = Guard.Core.Enums.NotificationTarget.Synchronization,
        OccurredAtUtc = DateTime.UtcNow.AddMinutes(-n)
      }));
      db.AuditEvents.AddRange(Enumerable.Range(1, 125).Select(n => new AuditEventRecord {
        EventId = Guid.NewGuid(), OccurredAtUtc = DateTime.UtcNow.AddMinutes(-n), SchemaVersion = 1,
        EventType = n % 2 == 0 ? Guard.Core.Enums.AuditEventType.LoginFailed : Guard.Core.Enums.AuditEventType.EntityChanged,
        Actor = $"browser-audit-{n:000}", ActorKind = "user", Outcome = n % 2 == 0 ? "failure" : "success",
        ObjectType = "Проверка", ObjectId = n.ToString(), CorrelationId = $"browser-operation-{n:000}",
        Details = "{\"Operation\":\"Modified\",\"Fields\":{\"IsActive\":{\"Before\":true,\"After\":false}}}",
        Attempts = n % 3, LastError = n % 3 == 2 ? "IOException" : null,
        SentAtUtc = n % 3 == 1 ? DateTime.UtcNow.AddMinutes(-n + 1) : null, NextAttemptAtUtc = DateTime.UtcNow.AddHours(1)
      }));
      var browserRoot = await db.Users.SingleAsync(u => u.Id == "root");
      browserRoot.PersonalId = people[2].Id;
      db.IpAddresses.AddRange(Enumerable.Range(0, 3).Select(n => new IpAddress {
        Address = $"198.51.100.{n + 1}", Description = $"Область браузера {n + 1}",
        SubdivisionId = subdivisions[n].Id, CreatedBy = "root"
      }));
      db.IpAddresses.AddRange(Enumerable.Range(1, 25).Select(n => new IpAddress {
        Address = $"203.0.113.{n}", Description = $"Проверка пагинации {n:00}", CreatedBy = "root"
      }));
      db.IpAddresses.AddRange(
        new IpAddress { Address = "203.0.113.101", Description = "Архивная запись", Status = Guard.Core.Enums.Status.Archived, CreatedBy = "root" },
        new IpAddress { Address = "203.0.113.102", Description = "Удалённая запись", Status = Guard.Core.Enums.Status.Deleted, CreatedBy = "root" });
      await db.SaveChangesAsync();
      await db.Database.ExecuteSqlRawAsync("CREATE TABLE logs (id uuid PRIMARY KEY DEFAULT gen_random_uuid(), timestamp timestamp without time zone, level varchar, layer varchar, message text, message_template text, exception text, properties jsonb, user_id varchar, ip_address varchar, correlation_id text, source_context text, diagnostic_session_id uuid, run_id text, fingerprint text, application_version text, instance_id text)");
      await db.Database.ExecuteSqlRawAsync("INSERT INTO logs (timestamp, level, layer, message, message_template, exception, properties, user_id, ip_address, correlation_id, source_context, fingerprint) SELECT (now() AT TIME ZONE 'UTC') - n * interval '1 minute', CASE WHEN n % 2 = 0 THEN 'Error' ELSE 'Information' END, 'Service', 'DTO log ' || lpad(n::text, 3, '0'), 'DTO log', NULL, jsonb_build_object(), 'test', '192.0.2.1', 'browser-correlation-' || n, 'Guard.Browser.Fixture', 'browser-fingerprint' FROM generate_series(1, 125) n");
    }
    var notificationSignals = new Guard.Core.Services.Notifications.NotificationSignals(Microsoft.Extensions.Logging.Abstractions.NullLogger<Guard.Core.Services.Notifications.NotificationSignals>.Instance);
    var notificationDispatcher = new Guard.Core.Services.Notifications.NotificationDispatcher(factory, notificationSignals);
    while (await notificationDispatcher.DeliverAsync() > 0) { }
    var exportDirectory = Path.Combine(Path.GetTempPath(), "guard-browser-export-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(exportDirectory);
    var start = new ProcessStartInfo("dotnet") {
      UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
      WorkingDirectory = exportDirectory
    };
    var testAssembly = Environment.GetEnvironmentVariable("GUARD_TEST_APP_DLL");
    var appAssembly = Path.GetFullPath(testAssembly ?? "Guard/bin/Debug/net10.0/Guard.dll");
    start.ArgumentList.Add(appAssembly);
    start.ArgumentList.Add("--urls"); start.ArgumentList.Add("http://127.0.0.1:5079");
    start.ArgumentList.Add("--contentRoot"); start.ArgumentList.Add(Path.GetFullPath("Guard"));
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    start.Environment["ASPNETCORE_STATICWEBASSETS"] = string.IsNullOrEmpty(testAssembly)
      ? Path.GetFullPath("Guard/bin/Debug/net10.0/Guard.staticwebassets.runtime.json")
      : Path.ChangeExtension(Path.GetFullPath(testAssembly), "staticwebassets.runtime.json");
    start.Environment["ConnectionStrings__DefaultConnection"] = isolatedConnection;
    start.Environment["ConnectionStrings__LogsConnection"] = isolatedConnection;
    start.Environment["Audit__Syslog__Host"] = "127.0.0.1";
    start.Environment["Audit__Syslog__Port"] = "9";
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
      await LocalAccountHttpTests.RunAsync(factory);
      await AuditAvailabilityHttpTests.RunAsync(factory);
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
