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
      // Сохраняющееся предупреждение подготавливается только во временной БД для проверки CompactNotice.
      await db.SynchronizationSettings.ExecuteUpdateAsync(s => s
        .SetProperty(x => x.Mode, DataManagementMode.Local).SetProperty(x => x.ScheduleEnabled, false)
        .SetProperty(x => x.RecoveryRequiredFrom, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3))));
      var subdivisions = Enumerable.Range(1, 27).Select(n => new Subdivision {
        Name = n == 26 ? "Архив отдела" : n == 27 ? "Удалённый отдел" : $"Поиск отдела {n:00}",
        Status = n == 26 ? Guard.Core.Enums.Status.Archived : n == 27 ? Guard.Core.Enums.Status.Deleted : Guard.Core.Enums.Status.Inserted,
        StaffCount = n * 1.5, LevelOrder = n, IsDepartment = n % 2 == 0, CreatedBy = "root"
      }).ToArray();
      db.AddRange(subdivisions); await db.SaveChangesAsync();
      // Синтетические длинные значения для таблицы, редактора и карточки подразделения.
      subdivisions[1].Name += " — " + string.Concat(Enumerable.Repeat("Длинное наименование для проверки интерфейса. ", 5));
      subdivisions[1].PositionFormationName = string.Concat(Enumerable.Repeat("Наименование подразделения для должности. ", 4));
      subdivisions[1].PostalCode = "000000";
      subdivisions[1].Address = string.Concat(Enumerable.Repeat("Тестовый адрес: длинная улица, дом 123, корпус 45. ", 12));
      subdivisions[1].Phone = "+0 (000) 000-00-00";
      subdivisions[1].Fax = "+0 (000) 000-00-01";
      // Та же длина текста в режиме просмотра без права изменения архивной записи.
      subdivisions[25].PositionFormationName = subdivisions[1].PositionFormationName;
      subdivisions[25].Address = subdivisions[1].Address;
      db.Classifiers.AddRange(Enumerable.Range(1, 25).Select(n => new Classifier {
        Type = 998812, Code = n, ClassifierName = n == 1 ? "Проверка DTO — " + string.Concat(Enumerable.Repeat("Длинное наименование. ", 8)) : "Проверка DTO",
        Value = n == 1 ? "Значение 01 — " + string.Concat(Enumerable.Repeat("Длинное значение для проверки интерфейса. ", 8)) : $"Значение {n:00}", IsActive = n % 2 == 0
      }));
      var longRoleName = "DTO роль 01 — " + string.Concat(Enumerable.Repeat("Длинное наименование роли. ", 7));
      db.Roles.AddRange(Enumerable.Range(1, 25).Select(n => new ApplicationRole {
        Name = n == 1 ? longRoleName : $"DTO роль {n:00}", NormalizedName = n == 1 ? longRoleName.ToUpperInvariant() : $"DTO РОЛЬ {n:00}", ConcurrencyStamp = Guid.NewGuid().ToString()
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
      // Длинные значения для проверки многоточия списка и переноса в карточке.
      people[0].MiddleName = "Оченьдлинноеотчестводляпроверкиотображенияитекстовойподсказки";
      people[0].FullName = string.Concat(Enumerable.Repeat("Синтетическое длинное ФИО для проверки отображения. ", 4));
      people[0].LastNameGen = "Проверки";
      people[0].FirstNameGen = "Браузера";
      people[0].PersonalNumber = "ui-standard-01";
      people[0].EnlistmentYear = 2020;
      db.NotificationEvents.AddRange(Enumerable.Range(1, 27).Select(n => new NotificationEvent {
        EventKey = $"browser-notification-{n:00}", UserId = "root", Kind = Guard.Core.Enums.UserNotificationKind.SynchronizationResult,
        Severity = Guard.Core.Enums.UserNotificationSeverity.Information,
        Title = n == 1 ? "Проверка уведомления 01 — " + string.Concat(Enumerable.Repeat("Длинный заголовок уведомления. ", 5)) : $"Проверка уведомления {n:00}",
        Message = n == 1 ? string.Concat(Enumerable.Repeat("Результат синтетической операции. Подробности с переносом по ширине карточки.\n", 20)) + new string('я', 400)
          : $"Результат фоновой операции {n:00} для проверки истории.", Target = Guard.Core.Enums.NotificationTarget.Synchronization,
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
    await using (var logsFixture = await factory.CreateDbContextAsync()) {
      var longLogMessage = string.Concat(Enumerable.Repeat("Длинное сообщение технического журнала. ", 16)) + """{"status":"synthetic"}""";
      var syntheticException = "System.InvalidOperationException: Синтетическая ошибка\n   at Guard.Browser.Fixture.Run()\n   at Guard.Browser.Fixture.Process()";
      await logsFixture.Database.ExecuteSqlInterpolatedAsync($"UPDATE logs SET message = {longLogMessage}, exception = {syntheticException} WHERE message = 'DTO log 001'");
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
    var runs = new List<(Process Process, Task<string> Output, Task<string> Error)>();
    Process StartApplication()
    {
      var child = Process.Start(start) ?? throw new InvalidOperationException("Не удалось запустить тестовый сервер.");
      runs.Add((child, child.StandardOutput.ReadToEndAsync(), child.StandardError.ReadToEndAsync()));
      return child;
    }
    var process = StartApplication();
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
      await using var preview = await UiStandardPreviewHost.StartAsync();
      Console.WriteLine("Synthetic UI preview: http://127.0.0.1:5080/ui-standard-preview");
      Console.WriteLine("Isolated browser server: http://127.0.0.1:5079 ; Root: root@browser.test / BrowserRoot123! ; press Enter to stop and remove temporary DB.");
      Console.WriteLine("Reconnect check: stop-web stops only the test application; start-web restarts it with the same temporary DB and keys.");
      while (await Console.In.ReadLineAsync() is { Length: > 0 } command) {
        if (command == "stop-web") {
          if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
          Console.WriteLine("Test application stopped; browser reconnect can now be checked.");
        }
        else if (command == "start-web" && process.HasExited) {
          process = StartApplication();
          Console.WriteLine("Test application starting on the same port.");
        }
        else if (command is "password-required" or "password-optional") {
          await using var previewDb = await factory.CreateDbContextAsync();
          await previewDb.Users.Where(u => u.Id == "root").ExecuteUpdateAsync(s => s.SetProperty(u => u.MustChangePassword, command == "password-required"));
          Console.WriteLine("Temporary Root password requirement updated for UI verification.");
        }
        else Console.WriteLine("Use stop-web, start-web, password-required, password-optional or Enter to finish.");
      }
    }
    finally {
      var logs = new System.Text.StringBuilder();
      foreach (var run in runs) {
        if (!run.Process.HasExited) run.Process.Kill(entireProcessTree: true);
        await run.Process.WaitForExitAsync();
        logs.Append(await run.Output).Append(await run.Error);
        run.Process.Dispose();
      }
      await File.WriteAllTextAsync("Guard/obj/account-policy-browser.log", logs.ToString());
      if (Directory.Exists(exportDirectory)) Directory.Delete(exportDirectory, true);
    }
  }
}
