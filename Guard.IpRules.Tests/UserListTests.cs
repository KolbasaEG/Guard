using Guard.Components.Pages.Administrator.Users;
using Guard.Core.Entities;
using Guard.Core.Services;
using Microsoft.EntityFrameworkCore;
using Radzen;
using Radzen.Blazor;

internal static class UserListTests
{
  public static async Task RunAsync(ISecurityService service, Action<bool, string> check)
  {
#pragma warning disable BL0005
    var condition = new CompositeFilterDescriptor { Property = "UserName", FilterOperator = FilterOperator.Contains, FilterValue = "policy" };
    var filter = new RadzenDataFilter<ApplicationUser> { Filters = [condition] };
#pragma warning restore BL0005
    var snapshot = UserListQuery.Capture(filter);
    condition.FilterValue = "missing-name";
    check(await service.QueryUsersAsync(q => snapshot(q).CountAsync()) == 1, "user filter snapshot is independent of editor");
    check(await service.QueryUsersAsync(q => UserListQuery.Capture(filter)(q).CountAsync()) == 0, "changed user filter needs new snapshot");
    var first = await service.QueryUsersAsync(q => UserListQuery.Sort(q, "Email desc").Skip(0).Take(1).Select(u => u.Id).SingleAsync());
    var second = await service.QueryUsersAsync(q => UserListQuery.Sort(q, "Email desc").Skip(1).Take(1).Select(u => u.Id).SingleAsync());
    check(first != second, "user pagination has stable unique tie-breaker");
    await PostgresTests.ThrowsAsync<ArgumentException>(() => Task.FromResult(UserListQuery.Sort(Array.Empty<ApplicationUser>().AsQueryable(), "PasswordHash asc")), check, "sensitive user sort fields rejected");
    condition.Property = "PasswordHash";
    await PostgresTests.ThrowsAsync<ArgumentException>(() => Task.FromResult(UserListQuery.Capture(filter)), check, "sensitive user filter fields rejected");
#pragma warning disable BL0005
    filter.Filters = [];
#pragma warning restore BL0005
    var cleared = UserListQuery.Capture(filter);
    check(await service.QueryUsersAsync(q => cleared(q).CountAsync()) == await service.QueryUsersAsync(q => q.CountAsync()), "clearing user filter restores authorized list");
  }
}
