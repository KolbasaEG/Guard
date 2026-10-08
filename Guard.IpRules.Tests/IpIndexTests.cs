using Guard.Components.Pages.Administrator.IpAddresses;
using Guard.Core.Entities;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;
using Radzen;
using Radzen.Blazor;

internal static class IpIndexTests
{
  public static async Task RunAsync(IIpAddressService service, Action<bool,string> check)
  {
#pragma warning disable BL0005 // Конструктор условий фильтра без браузерного renderer.
    var descriptor = new CompositeFilterDescriptor { Property = "Address", FilterOperator = FilterOperator.Contains, FilterValue = "192.168." };
    var filter = new RadzenDataFilter<IpAddress> { Filters = [descriptor] };
#pragma warning restore BL0005
    var snapshot = IpListQuery.Capture(filter);
    descriptor.FilterValue = "not-an-address";
    var result = await service.QueryIpAddressesAsync(async query => {
      query = snapshot(query);
      var count = await query.CountAsync();
      var rows = await IpListQuery.Sort(query, "Subdivision.Name asc, Address desc").Skip(0).Take(1)
        .Select(p => new IpAddressDto { Id=p.Id, Address=p.Address, Status=(int)p.Status,
          SubdivisionName=p.Subdivision != null ? p.Subdivision.Name : null }).ToListAsync();
      return (count, rows);
    });
    check(result.count > 0 && result.rows.Count == 1 && result.rows[0].Address.Contains("192.168."), "IP entity filter snapshot executes in SQL before DTO pagination");
    check(result.rows[0].SubdivisionName != null, "navigation sort and DTO projection execute without loading entity graph");
    check(await service.QueryIpAddressesAsync(q => IpListQuery.Capture(filter)(q).CountAsync()) == 0,
      "new filter snapshot sees changed condition; prior snapshot remains unchanged");
    await PostgresTests.ThrowsAsync<ArgumentException>(() => Task.FromResult(IpListQuery.Sort(Array.Empty<IpAddress>().AsQueryable(), "Personals.Count desc")), check, "IP sort rejects fields outside list contract");
    descriptor.Property = "Personals";
    await PostgresTests.ThrowsAsync<ArgumentException>(() => Task.FromResult(IpListQuery.Capture(filter)), check, "IP filter rejects fields outside list contract");
#pragma warning disable BL0005
    filter.LogicalFilterOperator = LogicalFilterOperator.Or;
    filter.Filters = [new() { Property="Address", FilterOperator=FilterOperator.Contains, FilterValue="192.168." },
      new() { Property="Address", FilterOperator=FilterOperator.Contains, FilterValue="2001:" }];
    var either = IpListQuery.Capture(filter);
    var alternatives = await service.QueryIpAddressesAsync(q => either(q).Select(ip => ip.Address).ToListAsync());
    check(alternatives.Any(a => a.Contains("192.168.")) && alternatives.Any(a => a.Contains("2001:")), "OR entity filters execute in PostgreSQL");
    filter.LogicalFilterOperator = LogicalFilterOperator.And;
    var both = IpListQuery.Capture(filter);
    check(await service.QueryIpAddressesAsync(q => both(q).CountAsync()) == 0, "AND applies both conditions before count");
    var names = new[] { "Own" };
    filter.Filters = [new() { Property="Subdivision.Name", Type=typeof(IEnumerable<string>), FilterOperator=FilterOperator.In, FilterValue=names }];
    var subdivisions = IpListQuery.Capture(filter);
    names[0] = "Other";
    var ownRows = await service.QueryIpAddressesAsync(q => subdivisions(q).Select(ip => ip.Subdivision!.Name).ToListAsync());
    check(ownRows.Count > 0 && ownRows.All(name => name == "Own"), "subdivision IN filter copies selection and executes in SQL");
    filter.Filters = [];
    var clear = IpListQuery.Capture(filter);
    check(await service.QueryIpAddressesAsync(q => clear(q).CountAsync()) == await service.QueryIpAddressesAsync(q => q.CountAsync()), "clear snapshot restores all authorized records");
#pragma warning restore BL0005
  }
}
