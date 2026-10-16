using Guard.Components.Pages.IpAddresses;
using Guard.Core.Entities;
using Guard.Core.Contexts;
using Guard.Core.Enums;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Radzen;
using Radzen.Blazor;

internal static class IpIndexTests
{
  public static async Task RunAsync(IIpAddressService service, IDbContextFactory<ApplicationDbContext> factory, Action<bool,string> check)
  {
#pragma warning disable BL0005 // Конструктор условий фильтра без браузерного renderer.
    var descriptor = new CompositeFilterDescriptor { Property = "Address", FilterOperator = FilterOperator.Contains, FilterValue = "192.168." };
    var filter = new RadzenDataFilter<IpAddress> { Filters = [descriptor] };
#pragma warning restore BL0005
    await using var db = await factory.CreateDbContextAsync();
    var rootScope = new ExportAccessScope(new("root", true, []), null, null, [], "test");
    foreach (var mode in Enum.GetValues<DataViewMode>())
    {
      var listIds = await service.QueryIpAddressesAsync(q => IpSearchQuery.ApplyVisibility(q, mode, true, true, null,
        SubdivisionHierarchyMode.CurrentOnly).OrderBy(ip => ip.Id).Select(ip => ip.Id).ToListAsync());
      var exportIds = await ExportQueries.IpAddresses(db, new(ExportArea.IpAddresses, ExportFormat.Excel, mode, AllSubdivisions: true), rootScope)
        .OrderBy(ip => ip.Id).Select(ip => ip.Id).ToListAsync();
      check(listIds.SequenceEqual(exportIds), "Root all-IP list and export contain identical records for each status mode");
    }
    var allRows = await service.QueryIpAddressesAsync(q => IpSearchQuery.ApplyVisibility(q, DataViewMode.Active, true, true, null,
      SubdivisionHierarchyMode.CurrentOnly).ToListAsync());
    check(allRows.Any(ip => ip.SubdivisionId == null), "Root all-IP SQL query includes common addresses");
    var ownPath = await db.Subdivisions.OrderBy(s => s.Id).Select(s => s.Path).FirstAsync();
    foreach (var hierarchy in Enum.GetValues<SubdivisionHierarchyMode>())
    {
      var scope = rootScope with { OwnPath = ownPath };
      var listIds = await service.QueryIpAddressesAsync(q => IpSearchQuery.ApplyVisibility(q, DataViewMode.Active, false, true, ownPath, hierarchy)
        .OrderBy(ip => ip.Id).Select(ip => ip.Id).ToListAsync());
      var exportIds = await ExportQueries.IpAddresses(db, new(ExportArea.IpAddresses, ExportFormat.Excel,
        IncludeChildren: hierarchy == SubdivisionHierarchyMode.IncludeChildren), scope).OrderBy(ip => ip.Id).Select(ip => ip.Id).ToListAsync();
      check(listIds.SequenceEqual(exportIds), "Root hierarchy IP list and export narrow full access identically");
    }
    await PostgresTests.ThrowsAsync<UnauthorizedAccessException>(() => Task.FromResult(ExportQueries.IpAddresses(db,
      new(ExportArea.IpAddresses, ExportFormat.Excel, AllSubdivisions: true), rootScope with { User = new("operator", false, []) })),
      check, "IP export rejects non-Root all-records request");
    var snapshot = IpListQuery.Capture(filter);
    descriptor.FilterValue = "not-an-address";
    var result = await service.QueryIpAddressesAsync(async query => {
      query = IpSearchQuery.Filter(query, snapshot);
      var count = await query.CountAsync();
      var rows = await IpSearchQuery.Sort(query, "Subdivision.Name asc, Address desc").Skip(0).Take(1)
        .Select(p => new IpAddressDto { Id=p.Id, Address=p.Address, Status=(int)p.Status,
          SubdivisionName=p.Subdivision != null ? p.Subdivision.Name : null }).ToListAsync();
      return (count, rows);
    });
    check(result.count > 0 && result.rows.Count == 1 && result.rows[0].Address.Contains("192.168."), "IP entity filter snapshot executes in SQL before DTO pagination");
    check(result.rows[0].SubdivisionName != null, "navigation sort and DTO projection execute without loading entity graph");
    check(await service.QueryIpAddressesAsync(q => IpSearchQuery.Filter(q, IpListQuery.Capture(filter)).CountAsync()) == 0,
      "new filter snapshot sees changed condition; prior snapshot remains unchanged");
    await PostgresTests.ThrowsAsync<ArgumentException>(() => Task.FromResult(IpSearchQuery.Sort(Array.Empty<IpAddress>().AsQueryable(), "Personals.Count desc")), check, "IP sort rejects fields outside list contract");
    descriptor.Property = "Personals";
    await PostgresTests.ThrowsAsync<ArgumentException>(() => Task.FromResult(IpListQuery.Capture(filter)), check, "IP filter rejects fields outside list contract");
#pragma warning disable BL0005
    filter.LogicalFilterOperator = LogicalFilterOperator.Or;
    filter.Filters = [new() { Property="Address", FilterOperator=FilterOperator.Contains, FilterValue="192.168." },
      new() { Property="Address", FilterOperator=FilterOperator.Contains, FilterValue="2001:" }];
    var either = IpListQuery.Capture(filter);
    var alternatives = await service.QueryIpAddressesAsync(q => IpSearchQuery.Filter(q, either).Select(ip => ip.Address).ToListAsync());
    check(alternatives.Any(a => a.Contains("192.168.")) && alternatives.Any(a => a.Contains("2001:")), "OR entity filters execute in PostgreSQL");
    filter.LogicalFilterOperator = LogicalFilterOperator.And;
    var both = IpListQuery.Capture(filter);
    check(await service.QueryIpAddressesAsync(q => IpSearchQuery.Filter(q, both).CountAsync()) == 0, "AND applies both conditions before count");
    var names = new[] { "Own" };
    filter.Filters = [new() { Property="Subdivision.Name", Type=typeof(IEnumerable<string>), FilterOperator=FilterOperator.In, FilterValue=names }];
    var subdivisions = IpListQuery.Capture(filter);
    names[0] = "Other";
    var ownRows = await service.QueryIpAddressesAsync(q => IpSearchQuery.Filter(q, subdivisions).Select(ip => ip.Subdivision!.Name).ToListAsync());
    check(ownRows.Count > 0 && ownRows.All(name => name == "Own"), "subdivision IN filter copies selection and executes in SQL");
    filter.Filters = [];
    var clear = IpListQuery.Capture(filter);
    check(await service.QueryIpAddressesAsync(q => IpSearchQuery.Filter(q, clear).CountAsync()) == await service.QueryIpAddressesAsync(q => q.CountAsync()), "clear snapshot restores all authorized records");
#pragma warning restore BL0005
  }
}
