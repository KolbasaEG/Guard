using Guard.Components.Library;
using Guard.Core.Services;
using Guard.Core.Services.Exports;
using Radzen.Blazor;

namespace Guard.Components.Pages.Users;

public static class UserListQuery
{
  public static ExportFilter Capture(RadzenDataFilter<UserFilterFields> filter, BrowserTimeService time) =>
    AdministrationListQuery.Capture(filter, time, UserSearchFilter.Fields, UserSearchFilter.Validate);
}
