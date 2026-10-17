using System.Net;
using Guard.Components.Library;
using Guard.Core.Identity;
using Guard.Core.Services;
using Guard.Core.Services.DTOs;
using Guard.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Radzen;

internal static class DialogInfoRenderingTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    await using var services = new ServiceCollection().AddLogging()
      .AddSingleton<IDirectoryManagementService, LocalManagement>()
      .AddSingleton<IJSRuntime, NoJs>().AddSingleton<NavigationManager, TestNavigation>()
      .AddSingleton<BrowserTimeService>().AddSingleton<NotificationService>().AddScoped<DialogService>()
      .BuildServiceProvider();
    await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
    await renderer.Dispatcher.InvokeAsync(async () => {
      foreach (var subdivision in new[] { false, true })
      foreach (var compact in new[] { false, true })
      foreach (var allowed in new[] { false, true })
      {
        var root = await renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(new Dictionary<string, object?> {
          [nameof(Host.Subdivision)] = subdivision, [nameof(Host.Compact)] = compact, [nameof(Host.Allowed)] = allowed
        }));
        var html = WebUtility.HtmlDecode(root.ToHtmlString());
        check(html.Contains("author@example.test") == allowed && html.Contains("editor@example.test") == allowed,
          $"service info preserves DTO authors and respects read access: subdivision={subdivision}, compact={compact}, allowed={allowed}");
        check(html.Contains(Host.RecordId.ToString()) == allowed,
          "service info renders the DTO identifier only with read access");
      }
    });
  }

  public sealed class Host : ComponentBase
  {
    public static readonly Guid RecordId = Guid.Parse("bc6d8145-7b60-4ce2-9468-3ea1adf09c97");
    [Parameter] public bool Subdivision { get; set; }
    [Parameter] public bool Compact { get; set; }
    [Parameter] public bool Allowed { get; set; }
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
      var info = new EntityInfoDto { Id = RecordId, InsertedDate = DateTime.UtcNow,
        Creator = "author@example.test", Editor = "editor@example.test" };
      var permission = Subdivision ? Permissions.Subdivisions.Read : Permissions.IpAddresses.Read;
      builder.OpenComponent<CascadingValue<UserAccessSnapshot>>(0);
      builder.AddAttribute(1, "Value", new UserAccessSnapshot("reader", false, Allowed ? [permission] : []));
      builder.AddAttribute(2, "ChildContent", (RenderFragment)(child => {
        child.OpenComponent<DialogInfo>(0);
        child.AddAttribute(1, nameof(DialogInfo.Compact), Compact);
        if (Subdivision) child.AddAttribute(2, nameof(DialogInfo.SubdivisionDetails), new SubdivisionDetailsDto { Info = info });
        else child.AddAttribute(3, nameof(DialogInfo.IpAddressDetails), new IpAddressDetailsDto { Info = info });
        child.CloseComponent();
      }));
      builder.CloseComponent();
    }
  }

  private sealed class LocalManagement : IDirectoryManagementService
  {
    public Task<bool> IsApiManagedAsync(CancellationToken ct = default) => Task.FromResult(false);
  }
  private sealed class NoJs : IJSRuntime
  {
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
  }
  private sealed class TestNavigation : NavigationManager
  {
    public TestNavigation() => Initialize("https://guard.test/", "https://guard.test/");
    protected override void NavigateToCore(string uri, bool forceLoad) { }
  }
}
