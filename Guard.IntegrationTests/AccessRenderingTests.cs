using Guard.Components.Layout.Components;
using Guard.Core.Identity;
using Guard.Core.Services.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class AccessRenderingTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var services = new ServiceCollection().AddLogging().BuildServiceProvider();
    await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
    Host? host = null;
    var routeRenders = 0;
    var parameters = ParameterView.FromDictionary(new Dictionary<string, object?> {
      [nameof(Host.Capture)] = (Action<Host>)(value => host = value),
      [nameof(Host.RouteChanged)] = (Action)(() => routeRenders++)
    });
    await renderer.Dispatcher.InvokeAsync(async () => {
      var root = await renderer.RenderComponentAsync<Host>(parameters);
      check(routeRenders == 1 && root.ToHtmlString().Contains("allowed"), "route initially renders with access");
      await host!.ChangeAsync(new("user", false, []));
      check(routeRenders == 1 && root.ToHtmlString().Contains("denied"), "access revocation reaches descendants without rerendering route");
      await host.ChangeAsync(new("user", false, [Permissions.Personals.Read]));
      check(routeRenders == 1 && root.ToHtmlString().Contains("allowed"), "restoring access preserves route instance");
    });
    await services.DisposeAsync();
  }

  public sealed class Host : ComponentBase
  {
    [Parameter] public Action<Host>? Capture { get; set; }
    [Parameter] public Action? RouteChanged { get; set; }
    private UserAccessSnapshot access = new("user", false, [Permissions.Personals.Read]);
    protected override void OnInitialized() => Capture?.Invoke(this);
    public Task ChangeAsync(UserAccessSnapshot value) => InvokeAsync(() => { access = value; StateHasChanged(); });
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
      builder.OpenComponent<CascadingValue<UserAccessSnapshot>>(0);
      builder.AddAttribute(1, "Value", access);
      builder.AddAttribute(2, "ChildContent", (RenderFragment)(child => {
        child.OpenComponent<StableAccessContent>(0);
        child.AddAttribute(1, "ChildContent", (RenderFragment)(route => {
          route.OpenComponent<RouteProbe>(0);
          route.AddAttribute(1, nameof(RouteProbe.Changed), RouteChanged);
          route.CloseComponent();
        }));
        child.CloseComponent();
      }));
      builder.CloseComponent();
    }
  }

  public sealed class RouteProbe : ComponentBase
  {
    [Parameter] public Action? Changed { get; set; }
    protected override void OnParametersSet() => Changed?.Invoke();
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
      builder.OpenComponent<PermissionProbe>(0);
      builder.CloseComponent();
    }
  }

  public sealed class PermissionProbe : ComponentBase
  {
    [CascadingParameter] public UserAccessSnapshot? Access { get; set; }
    protected override void BuildRenderTree(RenderTreeBuilder builder) =>
      builder.AddContent(0, Access?.Has(Permissions.Personals.Read) == true ? "allowed" : "denied");
  }
}
