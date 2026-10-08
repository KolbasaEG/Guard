using Guard.Components.Library;
using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Logging;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using Serilog;
using Serilog.Core;
using Serilog.Events;

internal static class RefactorTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql().Options;
    var logEvent = new LogEvent(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(3)), LogEventLevel.Information, null,
      new Serilog.Parsing.MessageTemplateParser().Parse("test"), []);
    var storedTime = new UtcTimestampColumnWriter().GetValue(logEvent, null);
    check(storedTime is DateTime storedDate && storedDate == logEvent.Timestamp.UtcDateTime && storedDate.Kind == DateTimeKind.Unspecified,
      "PostgreSQL timestamp without timezone receives UTC wall-clock value");
    using (var db = new ApplicationDbContext(options)) {
      foreach (var type in new[] { typeof(Personal), typeof(Subdivision), typeof(Classifier), typeof(OrganType) })
        check(db.Model.FindEntityType(type)!.FindProperty("Version")!.IsConcurrencyToken, $"concurrency mapping {type.Name}");
      check(!db.Database.HasPendingModelChanges(), "migration snapshot matches runtime model");
      var migrationSql = db.GetService<IMigrator>().GenerateScript(db.Database.GetMigrations().Single(m => m.EndsWith("_AddAccountPolicy")), "20261008160000_AddEntityVersions");
      check(migrationSql.Contains("IDENTITY") && migrationSql.Contains("setval") && migrationSql.Contains("MAX(\"Id\")"),
        "migration generates identity and advances sequence beyond existing organ type IDs");
      var projectionSql = db.Subdivisions.OrderBy(s => s.Name).ThenBy(s => s.Id).Take(20)
        .Select(s => new SubdivisionListDto { Id = s.Id, Name = s.Name, Version = s.Version,
          ParentName = s.Parent == null ? null : s.Parent.Name, OrganTypeName = s.OrganType == null ? null : s.OrganType.Name }).ToQueryString();
      check(projectionSql.Contains("LIMIT") && !projectionSql.Contains("CreatedBy"), "list projection and related names translate without loading full audit entity");
    }
    var contexts = new Contexts(options);
    await using var services = new ServiceCollection().BuildServiceProvider();
    var factory = new UnitOfWorkFactory(contexts, new PermissionsStub(), services, NullLogger<UnitOfWork>.Instance);
    await using (var first = await factory.CreateAsync())
    await using (var second = await factory.CreateAsync()) {
      var personal = new Personal { LastName = "Test", FirstName = "User" };
      await first.BaseEntityRepository<Personal>().AddAsync(personal);
      check(contexts.Created.Count == 2 && contexts.Created[0].ChangeTracker.Entries<Personal>().Count() == 1 &&
        !contexts.Created[1].ChangeTracker.Entries().Any(), "operation contexts and repositories are isolated");
      try { await first.SaveChangesAsync(); } catch (InvalidOperationException) { }
      check(personal.CreatedBy == "actor" && personal.InsertedDate.Kind == DateTimeKind.Utc, "factory writes retain caller audit before provider opens connection");
    }
    var target = new Personal { LastName = "old", FirstName = "old", CreatedBy = "creator", Status = Status.Blocked };
    var id = target.Id; var version = target.Version;
    new PersonalFieldsDto { LastName = "new", FirstName = "new" }.ApplyTo(target);
    check(target.Id == id && target.Version == version && target.CreatedBy == "creator" && target.Status == Status.Blocked,
      "command mapping preserves identity, version, audit and lifecycle");
    var submitted = 0;
    var submitRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
#pragma warning disable BL0005
    var form = new TestForm { Submit = Microsoft.AspNetCore.Components.EventCallback.Factory.Create(new object(), (Func<Task>)(async () => {
      submitted++; await submitRelease.Task;
    })) };
#pragma warning restore BL0005
    var firstSubmit = form.SubmitForTestAsync();
    await form.SubmitForTestAsync();
    check(submitted == 1, "duplicate form submission cannot start a second operation while save is awaiting");
    submitRelease.SetResult(); await firstSubmit;
    var time = new BrowserTimeService(new Js()); await time.InitializeAsync();
    check(time.ToUtc(new DateTime(2026, 1, 1, 12, 0, 0)) == new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc), "browser local filter converts to UTC");
#pragma warning disable BL0005
    var condition = new CompositeFilterDescriptor { Property = "Name", FilterOperator = FilterOperator.Contains, FilterValue = "old" };
    var filter = new RadzenDataFilter<Subdivision> { Filters = [condition] };
#pragma warning restore BL0005
    var snapshot = EntityListQuery<Subdivision>.Capture(filter, new HashSet<string> { "Name" }, time);
    condition.FilterValue = "new";
    var data = new[] { new Subdivision { Name = "old" }, new Subdivision { Name = "new" } }.AsQueryable();
    check(snapshot(data).Single().Name == "old", "applied filter is independent of edited descriptors");
    var dateCondition = new CompositeFilterDescriptor { Property = "LastActivityAtUtc", Type = typeof(DateTimeOffset?), FilterOperator = FilterOperator.Equals,
      FilterValue = new DateTime(2026, 1, 1) };
#pragma warning disable BL0005
    var dateFilter = new RadzenDataFilter<ApplicationUser> { Filters = [dateCondition] };
#pragma warning restore BL0005
    var day = EntityListQuery<ApplicationUser>.Capture(dateFilter, new HashSet<string> { "LastActivityAtUtc" }, time);
    var times = new[] {
      new ApplicationUser { Id = "start", LastActivityAtUtc = new DateTimeOffset(2025, 12, 31, 21, 0, 0, TimeSpan.Zero) },
      new ApplicationUser { Id = "end", LastActivityAtUtc = new DateTimeOffset(2026, 1, 1, 21, 0, 0, TimeSpan.Zero) }
    }.AsQueryable();
    check(day(times).Count() == 1 && day(times).First().Id == "start", "DateTimeOffset calendar day includes start and excludes next local midnight");
    dateCondition.FilterOperator = FilterOperator.GreaterThan;
    var afterDay = EntityListQuery<ApplicationUser>.Capture(dateFilter, new HashSet<string> { "LastActivityAtUtc" }, time);
    check(afterDay(times).Single().Id == "end", "date-only greater-than starts at next local midnight");
    var levels = new List<string> { "Error" };
#pragma warning disable BL0005
    var logFilter = new RadzenDataFilter<LogEntry> { Filters = [new() { Property = "Level", Type = typeof(IEnumerable<string>), FilterOperator = FilterOperator.In, FilterValue = levels }] };
#pragma warning restore BL0005
    var levelSnapshot = EntityListQuery<LogEntry>.Capture(logFilter, new HashSet<string> { "Level" }, time);
    levels.Clear(); levels.Add("Information");
    check(levelSnapshot(new[] { new LogEntry { Level = "Error" }, new LogEntry { Level = "Information" } }.AsQueryable()).Single().Level == "Error",
      "multiselect snapshot remains independent of changed selection collection");
    using (var db = new ApplicationDbContext(options)) {
      check(day(db.Users).ToQueryString().Contains("LastActivityAtUtc"), "browser date filter translates to PostgreSQL query");
      check(PersonalQueryFilter.Apply(db.Personals, new("Subdivision.Name", "Contains", "test")).ToQueryString().Contains("LIKE"),
        "explicit nested personal filter translates to PostgreSQL query");
    }
    var sorted = EntityListQuery<Subdivision>.Sort(data, "Name desc", new HashSet<string> { "Name" }, "Name asc");
    check(sorted.First().Name == "old", "supported sort direction is applied");
    try { EntityListQuery<Subdivision>.Sort(data, "CreatedBy", new HashSet<string> { "Name" }, "Name asc"); check(false, "unsupported sort rejected"); }
    catch (ArgumentException) { check(true, "unsupported sort rejected"); }
    var people = new[] { new Personal { FirstName = "Ivan", LastName = "One" }, new Personal { FirstName = "Petr", LastName = "Two" } }.AsQueryable();
    check(PersonalQueryFilter.Apply(people, new("FirstName", "Contains", "IVAN")).Single().FirstName == "Ivan", "personal search uses explicit case-insensitive criteria");
    try { PersonalQueryFilter.Apply(people, new("User.PasswordHash", "Equals", "value")); check(false, "unsupported personal filter rejected"); }
    catch (ArgumentException) { check(true, "unsupported personal filter rejected"); }
    var dst = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    foreach (var date in new[] { new DateTime(2026, 3, 8, 2, 30, 0), new DateTime(2026, 11, 1, 1, 30, 0) }) {
      try { BrowserTimeService.ConvertToUtc(date, dst); check(false, "ambiguous or absent local time rejected"); }
      catch (ArgumentException) { check(true, "ambiguous or absent local time rejected"); }
    }
    var sinks = new List<Sink>(); var level = new LoggingLevelSwitch(LogEventLevel.Information);
    using (var manager = new DynamicLoggerManager(level, (_, _) => {
      var sink = new Sink(); lock (sinks) { sinks.Add(sink); } return new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
    })) {
      manager.ApplyConfiguration(100, LogEventLevel.Information);
      var original = manager.Logger.ForContext("test", true);
      original.Information("before"); manager.ApplyConfiguration(200, LogEventLevel.Information); original.Information("after");
      check(sinks[0].Disposed && sinks[0].Events == 1 && sinks[1].Events == 1, "existing logger follows configuration and old sinks flush/dispose");
      try { manager.ApplyConfiguration(300, LogEventLevel.Debug, () => throw new IOException("test persistence failure")); }
      catch (IOException) { }
      original.Information("still active");
      check(level.MinimumLevel == LogEventLevel.Information && sinks[1].Events == 2 && sinks[2].Disposed,
        "persistence failure retains old configuration and releases candidate");
      await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => {
        original.Information("parallel"); manager.ApplyConfiguration(100, LogEventLevel.Information);
      })));
      check(sinks.Sum(s => s.Events) == 23, "concurrent configuration changes preserve emitted events");
    }
    check(sinks.All(s => s.Disposed), "all logger sinks are disposed at shutdown");
  }
  private sealed class Sink : ILogEventSink, IDisposable { public int Events; public bool Disposed; public void Emit(LogEvent e) { if (Disposed) throw new ObjectDisposedException(nameof(Sink)); Events++; } public void Dispose() => Disposed = true; }
  private sealed class TestForm : Guard.Components.Library.Forms.EntityForm<Personal> { public Task SubmitForTestAsync() => FormSubmit(); }
  private sealed class Contexts(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext> {
    public List<ApplicationDbContext> Created { get; } = [];
    public ApplicationDbContext CreateDbContext() { var db = new ApplicationDbContext(options); Created.Add(db); return db; }
  }
  private sealed class PermissionsStub : IPermissionService {
    public Task<UserAccessSnapshot> GetCurrentAsync(CancellationToken ct = default) => Task.FromResult(new UserAccessSnapshot("actor", true, []));
    public Task<UserAccessSnapshot> GetForUserAsync(string id, CancellationToken ct = default) => GetCurrentAsync(ct);
    public Task<bool> HasAsync(string code, CancellationToken ct = default) => Task.FromResult(true);
    public Task RequireAsync(string code, CancellationToken ct = default) => Task.CompletedTask;
  }
  private sealed class Js : IJSRuntime, IJSObjectReference {
    public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult((T)(identifier == "import" ? (object)this : "Europe/Moscow"));
    public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<T>(identifier, args);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }
}
