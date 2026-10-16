using System.Net;
using Guard.Components.Library.Authorization;
using Guard.Components.Library.Forms;
using Guard.Components.Pages.Subdivisions.Components;
using Guard.Core.Services.DTOs;
using Guard.Core.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

internal static class SubdivisionDialogsTests
{
  public static async Task RunAsync(Action<bool, string> check)
  {
    await using var services = new ServiceCollection().AddLogging()
      .AddSingleton<IJSRuntime, NoJs>().AddSingleton<NavigationManager, TestNavigation>().BuildServiceProvider();
    await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
    await renderer.Dispatcher.InvokeAsync(async () =>
    {
      Host? host = null;
      var root = await renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(new Dictionary<string, object?>
      {
        [nameof(Host.Capture)] = (Action<Host>)(value => host = value)
      }));
      string Html() => WebUtility.HtmlDecode(root.ToHtmlString());
      check(Html().Contains("<form") && Html().Contains("Черновик подразделения"), "active subdivision opens an editable form");
      await host!.ChangeAsync(true, false, Status.Modified);
      check(!Html().Contains("<form") && Html().Contains("<table") && Html().Contains("Черновик подразделения"),
        "revoking subdivision write access switches to a readable card without losing entered fields");
      await host.ChangeAsync(true, true, Status.Modified);
      check(Html().Contains("<form") && Html().Contains("Черновик подразделения"),
        "restoring subdivision write access preserves entered fields");
      foreach (var status in new[] { Status.Blocked, Status.Archived, Status.ArchivedBlocked, Status.Deleted })
      {
        await host.ChangeAsync(true, true, status);
        check(!Html().Contains("<form") && Html().Contains("<table"), $"subdivision {status} remains view-only for a writer");
      }
      await host.ChangeAsync(false, false, Status.Modified);
      check(!Html().Contains("Черновик подразделения") && !Html().Contains("<table"), "revoked subdivision read access hides the card contents");
      var add = await renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(new Dictionary<string, object?>
      {
        [nameof(Host.Adding)] = true
      }));
      var addHtml = WebUtility.HtmlDecode(add.ToHtmlString());
      check(addHtml.Contains("<form") && addHtml.Contains("ParentId") && addHtml.Contains("Fax"),
        "subdivision creation renders the parent selector and contact inputs in the shared form");
    });
  }

  public sealed class Host : ComponentBase
  {
    [Parameter] public Action<Host>? Capture { get; set; }
    [Parameter] public bool Adding { get; set; }
    private readonly SubdivisionDetailsDto item = new() { Name = "Черновик подразделения", Status = Status.Modified };
    private DialogAccessState access = new(true, true, null, () => Task.CompletedTask);
    protected override void OnInitialized() => Capture?.Invoke(this);
    public Task ChangeAsync(bool read, bool write, Status status) => InvokeAsync(() =>
    {
      item.Status = status;
      access = new(read, write, null, () => Task.CompletedTask);
      StateHasChanged();
    });
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
      builder.OpenComponent<CascadingValue<DialogAccessState>>(0);
      builder.AddAttribute(1, "Value", access);
      builder.AddAttribute(2, "ChildContent", (RenderFragment)(child =>
      {
        if (Adding)
        {
          child.OpenComponent<CreateEntity<SubdivisionDetailsDto>>(0);
          child.AddAttribute(1, "Data", item);
          child.AddAttribute(2, "ChildContent", (RenderFragment)(fields =>
          {
            fields.OpenComponent<SubdivisionFields>(0);
            fields.AddAttribute(1, "Item", item);
            fields.AddAttribute(2, "ShowParentSelector", true);
            fields.CloseComponent();
          }));
        }
        else
        {
          child.OpenComponent<SubdivisionDialogBody>(3);
          child.AddAttribute(4, "Item", item);
        }
        child.CloseComponent();
      }));
      builder.CloseComponent();
    }
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
