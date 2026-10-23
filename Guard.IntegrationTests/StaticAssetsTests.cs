using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class StaticAssetsTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    // Отдельный HTTP-host только для реальных ресурсов сборки: Program Guard и БД не запускаются.
    var manifestPath = Path.Combine(AppContext.BaseDirectory, "Guard.staticwebassets.endpoints.json");
    using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
    var routes = manifest.RootElement.GetProperty("Endpoints").EnumerateArray()
      .Select(endpoint => endpoint.GetProperty("Route").GetString()!).ToArray();
    var script = routes.First(route => route.StartsWith("_framework/blazor.web.") && route.EndsWith(".js"));
    var styles = routes.First(route => route.StartsWith("Guard.") && route.EndsWith(".styles.css"));

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
      ApplicationName = typeof(Guard.Components.App).Assembly.GetName().Name,
      EnvironmentName = "Development",
      ContentRootPath = AppContext.BaseDirectory
    });
    builder.Logging.ClearProviders();
    builder.WebHost.UseStaticWebAssets().UseUrls("http://127.0.0.1:0");
    await using var app = builder.Build();
    app.UseRouting();
    app.MapStaticAssets(manifestPath).ShortCircuit();
    app.Use(async (HttpContext context, RequestDelegate next) => {
      context.Response.StatusCode = StatusCodes.Status403Forbidden;
      await context.Response.WriteAsync("Protected request");
    });
    await app.StartAsync();
    try
    {
      var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
      using var client = new HttpClient { BaseAddress = new Uri(address) };
      using var js = await client.GetAsync(script);
      check(js.StatusCode == HttpStatusCode.OK && (await js.Content.ReadAsStringAsync()).Contains("Blazor"),
        "fingerprinted Blazor Web App bootstrap is served from the build manifest");
      using var css = await client.GetAsync(styles);
      check(css.StatusCode == HttpStatusCode.OK && (await css.Content.ReadAsStringAsync()).Contains(".app-shell"),
        "fingerprinted stylesheet contains the current isolated layout styles");
      // Холодные HTTP-запросы: шрифт и значки должны поставляться самим приложением.
      foreach (var font in new[] { "SourceSans3VF-Upright.ttf.woff2", "SourceSans3VF-Italic.ttf.woff2", "MaterialSymbolsOutlined.woff2" })
      {
        using var response = await client.GetAsync("_content/Radzen.Blazor/fonts/" + font);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        check(response.StatusCode == HttpStatusCode.OK && bytes.Length > 4 &&
          bytes.Take(4).SequenceEqual("wOF2"u8.ToArray()),
          $"offline font is served as WOFF2 from the local host: {font}");
      }
      using var layoutModule = await client.GetAsync("Components/Library/Templates/TableWrapperWithFilterAndAnalytics.razor.js");
      check(layoutModule.StatusCode == HttpStatusCode.OK && layoutModule.Content.Headers.ContentType?.MediaType?.Contains("javascript") == true,
        "table layout module is served locally with a JavaScript content type");
      foreach (var background in new[] { "images/home-light.png", "images/home-dark.png" })
      {
        using var response = await client.GetAsync(background);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        check(response.StatusCode == HttpStatusCode.OK && response.Content.Headers.ContentType?.MediaType == "image/png" &&
          bytes.Length > 8 && bytes.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
          $"theme background is served as a valid PNG: {background}");
      }
      check((await client.GetAsync("subdivisions")).StatusCode == HttpStatusCode.Forbidden &&
        (await client.GetAsync("_blazor")).StatusCode == HttpStatusCode.Forbidden,
        "static resource short circuit does not bypass middleware for pages and Blazor connections");
    }
    finally { await app.StopAsync(); }
    await ServicePagesTests.RunAsync(check);
  }
}
