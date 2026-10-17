using System.Reflection;
using System.Text.Json;
using Guard.Components.Library;
using Guard.Components.Pages.ExportArchive;
using Guard.Core.Entities;
using Guard.Core.Enums;
using Guard.Core.Services.Exports;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

internal static class ExportBoundaryTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    // Literal persisted version-1 request: enum numbers and filter JSON remain unchanged.
    var legacy = JsonSerializer.Deserialize<ExportRequest>("""
      {"Area":3,"Format":1,"Mode":1,"IncludeChildren":true,"AllSubdivisions":false,
       "OrderBy":"Address desc","Filter":{"All":true,"Children":[
         {"Field":"Address","Operation":"In","Value":["A"],"All":true,"Children":null}]},
       "PersonalFilter":null,"TimeZone":"Europe/Moscow"}
      """)!;
    DataExportService.Validate(legacy);
    var ips = new[] { new IpAddress { Address = "A" }, new IpAddress { Address = "B" } }.AsQueryable();
    check(legacy.Area == ExportArea.IpAddresses && legacy.IncludeChildren && legacy.TimeZone == "Europe/Moscow" &&
      ExportFilters.Apply(ips, legacy.Filter, ExportQueries.IpFields).Single().Address == "A",
      "persisted version-1 export JSON keeps enum, timezone and string-list filter semantics");

    var id = Guid.NewGuid(); var at = DateTimeOffset.Parse("2026-10-16T12:00:00Z");
    var rows = new[] {
      new ExportJob { Id = id, AuthorName = "Alpha", ClientIp = null, Area = ExportArea.IpAddresses, Format = ExportFormat.Excel,
        State = ExportJobState.Ready, RowCount = 100000, CreatedAtUtc = at },
      new ExportJob { AuthorName = "Beta", ClientIp = "192.0.2.1", Area = ExportArea.Personals, RowCount = 3, CreatedAtUtc = at.AddDays(-1) }
    }.AsQueryable();
    var filter = JsonSerializer.Deserialize<ExportFilter>("""
      {"All":true,"Children":[
        {"Field":"Area","Operation":"Equals","Value":3},
        {"All":false,"Children":[{"Field":"AuthorName","Operation":"Contains","Value":"ALP"},
          {"Field":"AuthorName","Operation":"In","Value":["absent"]}]},
        {"Field":"CreatedAtUtc","Operation":"GreaterThanOrEquals","Value":"2026-10-16T14:00:00+03:00"},
        {"Field":"RowCount","Operation":"GreaterThan","Value":99999},
        {"Field":"ClientIp","Operation":"IsNull","Value":null}]}
      """)!;
    ExportArchiveQuery.ValidateFilter(filter);
    check(ExportFilters.Apply(rows, filter, ExportArchiveQuery.Fields).Single().Id == id,
      "archive preserves nested groups, numeric enum, text case, null, long and offset-date semantics");
    var nested = new ExportFilter(Children: [new(All: false, Children: [
      new("AuthorName", "Equals", JsonSerializer.SerializeToElement("ALPHA")),
      new("AuthorName", "Equals", JsonSerializer.SerializeToElement("absent"))])]);
    ExportArchiveQuery.ValidateFilter(nested);
    check(ExportFilters.Apply(rows, nested, ExportArchiveQuery.Fields).Single().Id == id,
      "standalone nested OR group is applied rather than silently skipped");
    filter = new("Id", "Equals", JsonSerializer.SerializeToElement(id));
    ExportArchiveQuery.ValidateFilter(filter);
    check(ExportFilters.Apply(rows, filter, ExportArchiveQuery.Fields).Single().Id == id, "archive Guid filters retain typed values");
    foreach (var empty in new ExportFilter?[] { null, new(), new(Children: []) }) {
      ExportArchiveQuery.ValidateFilter(empty);
      check(ExportFilters.Apply(rows, empty, ExportArchiveQuery.Fields).Count() == 2, "cleared archive filter returns all authorized rows");
    }
    var deep = new ExportFilter("AuthorName", "Equals", JsonSerializer.SerializeToElement("A"));
    for (var n = 0; n < 18; n++) deep = new(Children: [deep]);
    foreach (var invalid in new[] {
      new ExportFilter("SecurityStamp", "Equals", JsonSerializer.SerializeToElement("secret")),
      new("AuthorName", "Custom", JsonSerializer.SerializeToElement("x")),
      new("Area", "Equals", JsonSerializer.SerializeToElement("IpAddresses")),
      new("RowCount", "Equals", JsonSerializer.SerializeToElement(new[] { 1, 2 })),
      new("CreatedAtUtc", "Equals", JsonSerializer.SerializeToElement("invalid-date")),
      new("CreatedAtUtc", "Contains", JsonSerializer.SerializeToElement("2026")),
      new(Children: [null!]), new(Children: Enumerable.Repeat(new ExportFilter(), 101).ToArray()), deep
    }) {
      try { ExportArchiveQuery.ValidateFilter(invalid); check(false, "invalid archive contract rejected without query construction"); }
      catch (ArgumentException) { check(true, "invalid archive contract rejected without query construction"); }
    }

    var time = new BrowserTimeService(new Js()); await time.InitializeAsync();
    var day = new DateTime(2026, 10, 16);
    var descriptor = new CompositeFilterDescriptor { Property = "CreatedAtUtc", FilterOperator = FilterOperator.Equals, FilterValue = day };
#pragma warning disable BL0005
    var builder = new RadzenDataFilter<ExportJob> { Filters = [descriptor] };
#pragma warning restore BL0005
    var snapshot = AdministrationListQuery.Capture(builder, time, ExportArchiveQuery.Fields, ExportArchiveQuery.ValidateFilter);
    descriptor.FilterValue = day.AddDays(1);
    var boundary = new[] {
      new ExportJob { CreatedAtUtc = DateTimeOffset.Parse("2026-10-15T20:59:59Z") },
      new ExportJob { CreatedAtUtc = DateTimeOffset.Parse("2026-10-15T21:00:00Z") },
      new ExportJob { CreatedAtUtc = DateTimeOffset.Parse("2026-10-16T20:59:59Z") },
      new ExportJob { CreatedAtUtc = DateTimeOffset.Parse("2026-10-16T21:00:00Z") }
    }.AsQueryable();
    var selectedDays = ExportFilters.Apply(boundary, snapshot, ExportArchiveQuery.Fields);
    check(selectedDays.Select(r => r.Id).SequenceEqual(boundary.Skip(1).Take(2).Select(r => r.Id)),
      "archive day filter captures independent half-open UTC range in browser timezone");
    await CheckRetentionFormAsync(check);
  }

  private static async Task CheckRetentionFormAsync(Action<bool, string> check)
  {
    var service = DispatchProxy.Create<IDataExportService, IpAddressReferenceTests.ServiceProxy>();
    var proxy = (IpAddressReferenceTests.ServiceProxy)(object)service;
    using var dialogs = new DialogService(new Navigation(), new Js());
    var closes = 0; dialogs.OnClose += _ => closes++;
    using var page = new Retention();
    typeof(Retention).GetProperty("Service", Flags)!.SetValue(page, service);
    typeof(Retention).GetProperty("Dialogs", Flags)!.SetValue(page, dialogs);
    typeof(Retention).GetProperty("Logger", Flags)!.SetValue(page, NullLogger<Retention>.Instance);
    proxy.Handle = (_, _) => Task.FromException<ExportRetentionDto>(new IOException());
    await Call(page, "OnInitializedAsync");
    var calls = 0;
    proxy.Handle = (_, _) => { calls++; return Task.CompletedTask; };
    await Call(page, "SaveAsync");
    check(calls == 0, "retention cannot save default values after failed load");
    var version = Guid.NewGuid();
    proxy.Handle = (_, _) => Task.FromResult(new ExportRetentionDto(4, version));
    await Call(page, "LoadAsync");
    var form = (Retention.RetentionForm)typeof(Retention).GetField("form", Flags)!.GetValue(page)!;
    form.Years = 5;
    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    CancellationToken token = default; var validCommand = false;
    proxy.Handle = (_, args) => { calls++; validCommand = (int)args[0]! == 5 && (Guid)args[1]! == version; token = (CancellationToken)args[2]!; return completion.Task; };
    var saving = Call(page, "SaveAsync"); await Call(page, "SaveAsync"); form.Years = 6;
    check(calls == 1 && validCommand, "retention sends loaded version and current value once while saving");
    page.Dispose(); completion.SetResult(); await saving;
    check(token.IsCancellationRequested && closes == 0, "closing retention cancels save and ignores its late completion");
  }
  private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
  private static Task Call(object page, string name) => (Task)page.GetType().GetMethod(name, Flags)!.Invoke(page, null)!;
  private sealed class Navigation : NavigationManager
  {
    public Navigation() => Initialize("https://guard.test/", "https://guard.test/");
    protected override void NavigateToCore(string uri, bool forceLoad) { }
  }
  private sealed class Js : IJSRuntime, IJSObjectReference
  {
    public ValueTask<T> InvokeAsync<T>(string id, object?[]? args) => ValueTask.FromResult((T)(id == "import" ? (object)this : "Europe/Moscow"));
    public ValueTask<T> InvokeAsync<T>(string id, CancellationToken ct, object?[]? args) => InvokeAsync<T>(id, args);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
  }
}
