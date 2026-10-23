using System.Net;
using Guard.Web.ServicePages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class ServicePagesTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    var builder = WebApplication.CreateBuilder();
    builder.Logging.ClearProviders();
    builder.WebHost.UseUrls("http://127.0.0.1:0");
    await using var app = builder.Build();
    app.UseGuardNotFoundPage();
    app.UseRouting();
    app.MapGet("/not-found", () => Results.Content("Страница не найдена", "text/html", statusCode: 404));
    foreach (var status in new[] { 401, 403, 404, 429, 500, 503 }) {
      var code = status;
      app.MapGet($"/known/{code}", () => Results.StatusCode(code));
    }
    await app.StartAsync();
    try {
      using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
      client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
      using var missing = await client.GetAsync("/missing-page?query=1");
      check(missing.StatusCode == HttpStatusCode.NotFound && (await missing.Content.ReadAsStringAsync()) == "Страница не найдена",
        "unknown HTML GET renders the service page and retains HTTP 404");
      foreach (var code in new[] { 401, 403, 404, 429, 500, 503 }) {
        using var response = await client.GetAsync($"/known/{code}");
        check((int)response.StatusCode == code && (await response.Content.ReadAsStringAsync()).Length == 0,
          $"known endpoint response {code} is not replaced by a not-found page");
      }
      using var post = await client.PostAsync("/missing-page", null);
      check(post.StatusCode == HttpStatusCode.NotFound && (await post.Content.ReadAsStringAsync()).Length == 0,
        "unknown POST is not re-executed as a page");
      foreach (var accept in new[] { "application/json", "text/css", "text/html;q=0", "*/*" }) {
        client.DefaultRequestHeaders.Accept.Clear(); client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        using var response = await client.GetAsync("/missing-resource");
        check(response.StatusCode == HttpStatusCode.NotFound && (await response.Content.ReadAsStringAsync()).Length == 0,
          $"non-page Accept {accept} keeps an empty 404 response");
      }
      client.DefaultRequestHeaders.Accept.Clear();
      using var noAccept = await client.GetAsync("/missing-resource");
      check(noAccept.StatusCode == HttpStatusCode.NotFound && (await noAccept.Content.ReadAsStringAsync()).Length == 0,
        "request without Accept is not converted into HTML");
    }
    finally { await app.StopAsync(); }
  }
}
