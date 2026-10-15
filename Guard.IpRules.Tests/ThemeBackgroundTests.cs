using Guard.Components.Pages;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen;

internal static class ThemeBackgroundTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    await using var services = new ServiceCollection().AddLogging()
      .AddSingleton<IJSRuntime, NoJs>().AddSingleton<ThemeService>().BuildServiceProvider();
    await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
    await renderer.Dispatcher.InvokeAsync(async () => {
      var themes = services.GetRequiredService<ThemeService>();
      themes.SetTheme("software", false);
      var home = await renderer.RenderComponentAsync<Home>();
      check(home.ToHtmlString().Contains("home-page-light"), "home uses the light background for the light software theme");
      themes.SetTheme("software-dark");
      check(home.ToHtmlString().Contains("home-page-dark") && !home.ToHtmlString().Contains("home-page-light"),
        "home switches to the dark background without navigation or page reload");
      themes.SetTheme("software");
      check(home.ToHtmlString().Contains("home-page-light"), "home switches back to the light background");
    });
  }

  private sealed class NoJs : IJSRuntime
  {
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => new(default(TValue)!);
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => new(default(TValue)!);
  }
}
