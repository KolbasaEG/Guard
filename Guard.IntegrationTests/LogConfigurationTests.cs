using Guard.Core.Identity;
using Guard.Core.Logging;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Core;
using Serilog.Events;

internal static class LogConfigurationTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var builds = 0;
    using var manager = new DynamicLoggerManager(new LoggingLevelSwitch(), (_, _) => {
      builds++; return new LoggerConfiguration().CreateLogger();
    });
    var access = new Access();
    var audit = new AuditCapture();
    var service = new LogConfigurationService(access, manager, NullLogger<LogConfigurationService>.Instance, audit);
    foreach (var level in Enum.GetValues<LogEventLevel>()) {
      manager.ApplyConfiguration(217, level);
      var result = await service.GetAsync();
      check(result.MaxSessions == 217 && result.MinimumLevel.ToString() == level.ToString(),
        $"configuration read maps {level} from active manager, without reading settings file");
    }
    var before = builds;
    foreach (var settings in new[] { new LogConfigurationDto(0, ApplicationLogLevel.Error),
      new(10001, ApplicationLogLevel.Error), new(1, (ApplicationLogLevel)(-1)), new(1, (ApplicationLogLevel)123) })
      await PostgresTests.ThrowsAsync<ArgumentException>(() => service.SaveAsync(settings), check,
        "invalid settings rejected before creating logger or persisting settings");
    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
    await PostgresTests.ThrowsAsync<OperationCanceledException>(() => service.GetAsync(cancelled.Token), check,
      "cancelled configuration read does not return active settings");
    foreach (var level in Enum.GetValues<ApplicationLogLevel>())
      await PostgresTests.ThrowsAsync<OperationCanceledException>(() => service.SaveAsync(new(1, level), cancelled.Token), check,
        $"valid {level} mapping reaches cancellation before changing runtime or file");
    access.Allowed = false;
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(), check, "configuration read requires Logs.Read");
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync(new(1, ApplicationLogLevel.Error)), check, "configuration write requires Logs.Configure");
    check(builds == before && manager.GetConfiguration() == (217, LogEventLevel.Fatal), "rejected configuration operations retain active settings");
    access.Allowed = true; access.CurrentRoot = false;
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync(new(1, ApplicationLogLevel.Error)), check,
      "configuration save rechecks actor access after permission check");
    check(builds == before, "lost access never constructs a replacement logger");

    var fake = new Settings();
    await using var page = new Page(fake);
    await page.Save();
    check(fake.Saves == 0, "log settings cannot save before successful load");
    fake.FailLoad = true; await page.Load(); await page.Save();
    check(fake.Saves == 0, "failed log settings load cannot overwrite with defaults");
    fake.FailLoad = false; await page.Load();
    check(page.Values == (231, ApplicationLogLevel.Warning), "log settings page applies returned DTO");
    var pending = page.Save(); await page.Save();
    page.Edit(999, ApplicationLogLevel.Fatal);
    check(fake.Saves == 1 && fake.Command == new LogConfigurationDto(231, ApplicationLogLevel.Warning), "log settings save captures immutable values and suppresses duplicate save");
    fake.Completion.SetResult(new(232, ApplicationLogLevel.Error)); await pending;
    check(page.Values == (232, ApplicationLogLevel.Error), "log settings page applies confirmed server result");
    fake.Completion = new();
    pending = page.Save(); await page.DisposeAsync();
    fake.Completion.SetResult(new(777, ApplicationLogLevel.Debug)); await pending;
    check(fake.Token.IsCancellationRequested && page.Values == (232, ApplicationLogLevel.Error), "disposed log settings cancel save and ignore late result");
    var slow = new Settings { LoadCompletion = new() };
    await using var closed = new Page(slow);
    var loading = closed.Load(); await closed.DisposeAsync();
    slow.LoadCompletion.SetResult(new(888, ApplicationLogLevel.Debug)); await loading;
    check(slow.Token.IsCancellationRequested && closed.Values == (100, ApplicationLogLevel.Information), "disposed log settings cancel read and ignore late DTO");
  }

  private sealed class AuditCapture : IAuditService
  {
    public readonly List<string?> Events = [];
    public Task RecordAsync(Guard.Core.Services.AuditEventData entry, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); LogIdentityEvent(entry.EventType, entry.Actor, entry.ClientIp, entry.Details); return Task.CompletedTask; }
    public void LogIdentityEvent(Guard.Core.Enums.AuditEventType type, string? actor, string? ip = null, string? details = null) => Events.Add(details);
  }
  private sealed class Access : IPermissionService
  {
    public bool Allowed = true;
    public bool CurrentRoot = true;
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(new UserAccessSnapshot("actor", CurrentRoot, []));
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string code, CancellationToken ct = default) => Task.FromResult(Allowed);
    public Task RequireAsync(string code, CancellationToken ct = default) => Allowed ? Task.CompletedTask : Task.FromException(new UnauthorizedAccessException());
  }
  private sealed class Settings : ILogConfigurationService
  {
    public bool FailLoad;
    public int Saves;
    public LogConfigurationDto? Command;
    public CancellationToken Token;
    public TaskCompletionSource<LogConfigurationDto> Completion = new();
    public TaskCompletionSource<LogConfigurationDto>? LoadCompletion;
    public Task<LogConfigurationDto> GetAsync(CancellationToken ct = default) {
      Token = ct;
      return FailLoad ? Task.FromException<LogConfigurationDto>(new IOException())
        : LoadCompletion?.Task ?? Task.FromResult(new LogConfigurationDto(231, ApplicationLogLevel.Warning));
    }
    public Task<LogConfigurationDto> SaveAsync(LogConfigurationDto settings, CancellationToken ct = default) {
      Saves++; Command = settings; Token = ct; return Completion.Task;
    }
  }
  private sealed class Page : Guard.Components.Pages.Logs.Index
  {
    public Page(ILogConfigurationService service) { Configuration = service; Logger = NullLogger<Guard.Components.Pages.Logs.Index>.Instance; NotificationService = new(); }
    public Task Load() => OnInitializedAsync();
    public Task Save() => SaveLoggerSettings();
    public (int, ApplicationLogLevel) Values => (maxSessions, selectedLogLevel);
    public void Edit(int sessions, ApplicationLogLevel level) { maxSessions = sessions; selectedLogLevel = level; }
  }
}
