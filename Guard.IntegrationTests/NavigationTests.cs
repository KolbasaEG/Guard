using System.Reflection;
using Guard.Components.Layout.Components;
using Guard.Components.Layout.Navigation;
using Guard.Core.Identity;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

internal static class NavigationTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var root = new UserAccessSnapshot("root", true, []);
    var limited = new UserAccessSnapshot("user", false, [Permissions.Personals.Read]);
    var limitedItems = NavigationCatalog.VisibleGroups(limited).SelectMany(group => group.Items).ToArray();
    check(limitedItems.Select(item => item.Path).ToHashSet().SetEquals(["/", "/Notifications", "/personal"]),
      "navigation limits an ordinary user to permitted sections");
    check(!NavigationCatalog.VisibleGroups(new("user", false, [])).Any(group => group.Text != "Обзор"),
      "navigation omits groups with no available sections");
    check(!NavigationCatalog.VisibleGroups(null).Any(), "navigation is empty before access is available");
    check(!NavigationCatalog.VisibleGroups(new("user", false, [Permissions.Roles.Read, Permissions.Synchronization.Run]))
      .SelectMany(group => group.Items).Any(item => item.Path is "/roles" or "/synchronization" or "/account-policy"),
      "legacy claims do not expose Root-only sections");

    var routes = typeof(Guard.Components.Pages.Home).Assembly.GetTypes()
      .SelectMany(type => type.GetCustomAttributes<RouteAttribute>().Select(route => (route.Template, Type: type)))
      .ToArray();
    foreach (var item in NavigationCatalog.VisibleGroups(root).SelectMany(group => group.Items))
    {
      var targets = routes.Where(route => route.Template.Equals(item.Path, StringComparison.OrdinalIgnoreCase)).ToArray();
      check(targets.Length == 1, $"navigation resolves a unique existing route: {item.Path}");
      check(targets[0].Type.GetCustomAttributes<AuthorizeAttribute>(true).Any(), $"route remains authorized: {item.Path}");
      if (item.Permission != null)
        check(targets[0].Type.GetCustomAttributes<AuthorizeAttribute>(true).Any(auth => auth.Policy == item.Permission),
          $"route preserves its permission policy: {item.Path}");
    }
    check(routes.Single(route => route.Template == "/AdminPannel").Type == typeof(Guard.Components.Pages.Home),
      "old administrator landing URL resolves to the new home page");
    check(NavigationCatalog.Find("PERSONAL/?page=2#table")?.Path == "/personal" &&
      NavigationCatalog.Find("AdminPannel")?.Path == "/", "header identifies direct links and legacy landing route");

    await using var services = new ServiceCollection().AddLogging()
      .AddSingleton<NavigationManager, NavigationStub>()
      .AddSingleton<IJSRuntime, NoJs>().BuildServiceProvider();
    await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
    await renderer.Dispatcher.InvokeAsync(async () => {
      Host? host = null;
      NavigationProbe? menu = null;
      var restrictedRoot = new UserAccessSnapshot("root", false, [Permissions.Personals.Read]);
      var rendered = await renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(new Dictionary<string, object?> {
        [nameof(Host.Capture)] = (Action<Host>)(value => host = value),
        [nameof(Host.CaptureMenu)] = (Action<NavigationProbe>)(value => menu = value)
      }));
      check(rendered.ToHtmlString().Contains("href=\"/synchronization\""), "Root menu renders system sections");
      check(rendered.ToHtmlString().Contains("<details") && rendered.ToHtmlString().Contains("<summary"),
        "static Identity rendering provides native expandable navigation without Blazor event handlers");
      await menu!.SearchAsync("СИНХРОНИЗАЦИЯ");
      var searched = rendered.ToHtmlString();
      check(searched.Contains("href=\"/synchronization\"") && !searched.Contains("href=\"/personal\""),
        "menu search finds sections across groups and ignores case");
      await menu.SearchAsync("раздел которого нет");
      check(System.Net.WebUtility.HtmlDecode(rendered.ToHtmlString()).Contains("Раздел не найден"),
        "empty menu search shows an explicit result");
      await menu.SearchAsync("");
      await menu.FavoriteAsync("/synchronization");
      check(rendered.ToHtmlString().Split("href=\"/synchronization\"").Length == 3,
        "favorite section appears both in its group and in quick navigation");
      await host!.ChangeAsync(restrictedRoot);
      var html = rendered.ToHtmlString();
      check(html.Contains("href=\"/personal\"") && !html.Contains("href=\"/synchronization\"") && !html.Contains("href=\"/roles\""),
        "revoking access updates the actual menu inside stable routed content");
      await host.ChangeAsync(root);
      check(rendered.ToHtmlString().Contains("href=\"/synchronization\""), "restored permissions restore menu items");
      await menu.SearchAsync("Система");
      check(rendered.ToHtmlString().Contains("href=\"/Logs\"") && !rendered.ToHtmlString().Contains("href=\"/personal\""),
        "search by group name reveals matching permitted sections");
      await host.ChangeAsync(restrictedRoot);
      check(!rendered.ToHtmlString().Contains("href=\"/synchronization\""),
        "search and favorites cannot expose revoked sections");
      await menu.FavoriteAsync("/synchronization");
      await host.ChangeAsync(root);
      await menu.SearchAsync("");
      check(rendered.ToHtmlString().Split("href=\"/synchronization\"").Length == 3,
        "revoked favorite cannot be changed and is restored with permission");
      await host.ChangeAsync(new("other-user", false, [Permissions.Personals.Read]));
      await menu.FavoriteAsync("/personal");
      await host.ChangeAsync(root);
      check(rendered.ToHtmlString().Split("href=\"/personal\"").Length == 2,
        "switching accounts clears previous account favorite state");
      await menu.SearchAsync("Персонал");
      services.GetRequiredService<NavigationManager>().NavigateTo("/users");
      check(rendered.ToHtmlString().Contains("href=\"/synchronization\""),
        "navigation clears the search and restores grouped menu");
    });
  }

  public sealed class Host : ComponentBase
  {
    [Parameter] public Action<Host>? Capture { get; set; }
    [Parameter] public Action<NavigationProbe>? CaptureMenu { get; set; }
    private UserAccessSnapshot access = new("root", true, []);
    protected override void OnInitialized() => Capture?.Invoke(this);
    public Task ChangeAsync(UserAccessSnapshot value) => InvokeAsync(() => { access = value; StateHasChanged(); });
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
      builder.OpenComponent<CascadingValue<UserAccessSnapshot>>(0);
      builder.AddAttribute(1, "Value", access);
      builder.AddAttribute(2, "ChildContent", (RenderFragment)(child => {
        child.OpenComponent<StableAccessContent>(0);
        child.AddAttribute(1, "ChildContent", (RenderFragment)(content => {
          content.OpenComponent<NavigationProbe>(0);
          content.AddComponentReferenceCapture(1, component => CaptureMenu?.Invoke((NavigationProbe)component));
          content.CloseComponent();
        }));
        child.CloseComponent();
      }));
      builder.CloseComponent();
    }
  }

  public sealed class NavigationProbe : AppNavigation
  {
    public Task SearchAsync(string value) => InvokeAsync(() => { SearchChanged(value); StateHasChanged(); });
    public Task FavoriteAsync(string path) => InvokeAsync(async () => {
      await ToggleFavoriteAsync(NavigationCatalog.Groups.SelectMany(group => group.Items).Single(item => item.Path == path));
      StateHasChanged();
    });
  }

  private sealed class NavigationStub : NavigationManager
  {
    public NavigationStub() => Initialize("https://guard.test/", "https://guard.test/personal");
    protected override void NavigateToCore(string uri, bool forceLoad)
    {
      Uri = ToAbsoluteUri(uri).AbsoluteUri;
      NotifyLocationChanged(false);
    }
  }

  private sealed class NoJs : IJSRuntime
  {
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => new(default(TValue)!);
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => new(default(TValue)!);
  }
}
