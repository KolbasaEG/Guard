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

    var cookies = new CookieJs();
    await using var cookieServices = new ServiceCollection().AddLogging()
      .AddSingleton<IJSRuntime>(cookies).AddScoped<ThemeService>()
      .AddRadzenCookieThemeService(options => { options.Name = "ApplicationTheme"; options.Duration = TimeSpan.FromDays(365); })
      .BuildServiceProvider();
    await using (var firstPage = cookieServices.CreateAsyncScope())
    {
      _ = firstPage.ServiceProvider.GetRequiredService<CookieThemeService>();
      firstPage.ServiceProvider.GetRequiredService<ThemeService>().SetTheme("software-dark");
      check(cookies.Cookie.StartsWith("ApplicationTheme=software-dark;"), "theme changes persist to the configured application cookie");
    }
    await using (var reloadedPage = cookieServices.CreateAsyncScope())
    {
      _ = reloadedPage.ServiceProvider.GetRequiredService<CookieThemeService>();
      var restored = reloadedPage.ServiceProvider.GetRequiredService<ThemeService>();
      check(restored.Theme == "software-dark", "a new circuit restores the saved dark theme");
      restored.SetTheme("software");
      check(cookies.Cookie.StartsWith("ApplicationTheme=software;"), "switching back to the light theme updates the cookie");
    }
    await using (var reloadedLightPage = cookieServices.CreateAsyncScope())
    {
      _ = reloadedLightPage.ServiceProvider.GetRequiredService<CookieThemeService>();
      check(reloadedLightPage.ServiceProvider.GetRequiredService<ThemeService>().Theme == "software", "a new circuit restores the saved light theme");
    }
  }

  private sealed class NoJs : IJSRuntime
  {
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => new(default(TValue)!);
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => new(default(TValue)!);
  }

  private sealed class CookieJs : IJSRuntime
  {
    public string Cookie { get; private set; } = "";
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
      if (identifier == "Radzen.getCookies") return new((TValue)(object)Cookie.Split(';')[0]);
      if (identifier == "Radzen.setCookie") Cookie = (string)args![0]!;
      return new(default(TValue)!);
    }
  }
}
