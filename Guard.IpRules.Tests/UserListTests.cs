using Guard.Components.Pages.Users;
using Guard.Components.Library;
using Guard.Core.Services;
using Radzen;
using Radzen.Blazor;

internal static class UserListTests
{
  public static async Task RunAsync(ISecurityService service, Action<bool, string> check)
  {
#pragma warning disable BL0005
    var condition = new CompositeFilterDescriptor { Property = "UserName", FilterOperator = FilterOperator.Contains, FilterValue = "policy" };
    var filter = new RadzenDataFilter<UserFilterFields> { Filters = [condition] };
#pragma warning restore BL0005
    var snapshot = UserListQuery.Capture(filter, new BrowserTimeService(null!));
    condition.FilterValue = "missing-name";
    check((await service.SearchUsersAsync(new(Filter: snapshot))).Count == 1, "user filter snapshot is independent of editor");
    check((await service.SearchUsersAsync(new(Filter: UserListQuery.Capture(filter, new BrowserTimeService(null!))))).Count == 0, "changed user filter needs new snapshot");
    var first = (await service.SearchUsersAsync(new(Take: 1, OrderBy: "Email desc"))).Items.Single().Id;
    var second = (await service.SearchUsersAsync(new(Skip: 1, Take: 1, OrderBy: "Email desc"))).Items.Single().Id;
    check(first != second, "user pagination has stable unique tie-breaker");
    await PostgresTests.ThrowsAsync<ArgumentException>(() => service.SearchUsersAsync(new(OrderBy: "PasswordHash asc")), check, "sensitive user sort fields rejected");
    condition.Property = "PasswordHash";
    await PostgresTests.ThrowsAsync<ArgumentException>(() => Task.FromResult(UserListQuery.Capture(filter, new BrowserTimeService(null!))), check, "sensitive user filter fields rejected");
#pragma warning disable BL0005
    filter.Filters = [];
#pragma warning restore BL0005
    var cleared = UserListQuery.Capture(filter, new BrowserTimeService(null!));
    check((await service.SearchUsersAsync(new(Filter: cleared))).Count == (await service.SearchUsersAsync(new())).Count, "clearing user filter restores authorized list");
  }
}
