using Guard.Components.Library.Templates;
using Guard.Components.Library.Views;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radzen;
using Radzen.Blazor;

// Только Browser-стенд: синтетическое содержимое не включается в production-сборку.
internal static class UiStandardPreviewHost
{
  public static async Task<WebApplication> StartAsync()
  {
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
      ApplicationName = typeof(Guard.Components.App).Assembly.GetName().Name,
      EnvironmentName = "Development", ContentRootPath = Path.GetFullPath("Guard")
    });
    builder.Logging.ClearProviders();
    builder.WebHost.UseStaticWebAssets().UseUrls("http://127.0.0.1:5080");
    builder.Services.AddRazorComponents().AddInteractiveServerComponents();
    builder.Services.AddRadzenComponents();
    var app = builder.Build();
    app.UseAntiforgery();
    app.MapStaticAssets(Path.Combine(AppContext.BaseDirectory, "Guard.staticwebassets.endpoints.json"));
    app.MapRazorComponents<UiStandardPreviewRoot>().AddInteractiveServerRenderMode();
    await app.StartAsync();
    return app;
  }
}

public sealed class UiStandardPreviewRoot : ComponentBase
{
  protected override void BuildRenderTree(RenderTreeBuilder b)
  {
    b.OpenElement(0, "html"); b.AddAttribute(1, "lang", "ru");
    b.OpenElement(2, "head");
    b.AddMarkupContent(3, "<base href='/'><meta charset='utf-8'><title>Проверка оболочек Guard</title>" +
      "<link rel='stylesheet' href='/_content/Radzen.Blazor/css/software-base.css'>" +
      "<link rel='stylesheet' href='/ui-standard.css'><link rel='stylesheet' href='/Guard.styles.css'>");
    b.CloseElement(); b.OpenElement(4, "body");
    b.OpenComponent<UiStandardPreview>(5); b.AddComponentRenderMode(new InteractiveServerRenderMode()); b.CloseComponent();
    b.AddMarkupContent(6, "<script src='/_framework/blazor.web.js'></script><script src='/_content/Radzen.Blazor/Radzen.Blazor.js'></script>");
    b.CloseElement(); b.CloseElement();
  }
}

[Route("/ui-standard-preview")]
public sealed class UiStandardPreview : ComponentBase
{
  [Inject] public DialogService Dialog { get; set; } = default!;
  [Inject] public ThemeService Theme { get; set; } = default!;
  private bool dark;
  private string confirmationResult = "Подтверждение ещё не открывалось";

  private async Task ConfirmArchiveAsync()
  {
    var result = await Dialog.Confirm("Вы действительно хотите поместить запись в архив?", "Архивирование",
      new ConfirmOptions { OkButtonText = "Да", CancelButtonText = "Отмена" });
    confirmationResult = result == true ? "Подтверждено" : "Отменено";
  }

  protected override void BuildRenderTree(RenderTreeBuilder b)
  {
    b.OpenComponent<RadzenTheme>(0); b.AddAttribute(1, "Theme", dark ? "software-dark" : "software"); b.CloseComponent();
    b.OpenElement(2, "div"); b.AddAttribute(3, "class", "app-shell");
    b.AddAttribute(4, "style", "height:calc(100vh - 16px);min-height:0;display:flex;flex-direction:column;");
    b.OpenComponent<RadzenComponents>(40); b.CloseComponent();
    b.OpenComponent<RadzenButton>(50); b.AddAttribute(51, "Text", "Проверить отказ в доступе");
    b.AddAttribute(52, "Click", EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this,
      async () => { await Dialog.OpenAsync<UiStandardAccessDeniedDialog>("", null, new DialogOptions {
        Width = "640px", ShowTitle = false, ContentCssClass = "guard-entity-dialog-content"
      }); })); b.CloseComponent();
    b.OpenComponent<RadzenButton>(5); b.AddAttribute(6, "Text", "Сменить тему тестового экрана");
    b.AddAttribute(7, "Click", EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, () => {
      dark = !dark;
      Theme.SetTheme(dark ? "software-dark" : "software");
    })); b.CloseComponent();
    b.OpenComponent<RadzenButton>(41); b.AddAttribute(42, "Text", "Проверить длинный диалог");
    b.AddAttribute(43, "Click", EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this,
      async () => { await Dialog.OpenAsync<UiStandardLongDialog>("", null, new DialogOptions {
        Width = "640px", ShowTitle = false, ContentCssClass = "guard-ip-dialog-content"
      }); })); b.CloseComponent();
    b.OpenComponent<RadzenButton>(44); b.AddAttribute(45, "Text", "Проверить подтверждение архивации");
    b.AddAttribute(46, "Click", EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, ConfirmArchiveAsync)); b.CloseComponent();
    b.OpenElement(47, "output"); b.AddAttribute(48, "aria-label", "Результат подтверждения");
    b.AddContent(49, confirmationResult); b.CloseElement();
    b.OpenElement(8, "div"); b.AddAttribute(9, "style", "flex:1;min-height:0;");
    b.OpenComponent<TableWrapperWithFilterAndAnalytics>(10);
    b.AddAttribute(11, "Analitycs", (RenderFragment)(a => {
      for (var n = 0; n < 50; n++) { a.OpenElement(0, "p"); a.AddContent(1, $"Синтетическая строка аналитики {n}: Ёжик, проверка переноса длинногослова" + new string('я', 160)); a.CloseElement(); }
    }));
    b.AddAttribute(12, "Filter", (RenderFragment)(f => {
      f.OpenComponent<RadzenDataFilter<PreviewFields>>(0);
      f.AddAttribute(1, "Auto", false); f.AddAttribute(2, "Data", Array.Empty<PreviewFields>());
      f.AddAttribute(3, "AddFilterText", "Добавить фильтр");
      f.AddAttribute(4, "AddFilterGroupText", "Добавить группу фильтров");
      f.AddAttribute(5, "Properties", (RenderFragment)(p => {
        p.OpenComponent<RadzenDataFilterProperty<PreviewFields>>(0);
        p.AddAttribute(1, "Property", "Name"); p.AddAttribute(2, "Title", "Название"); p.CloseComponent();
        p.OpenComponent<RadzenDataFilterProperty<PreviewFields>>(3);
        p.AddAttribute(4, "Property", "Description"); p.AddAttribute(5, "Title", "Описание"); p.CloseComponent();
      }));
      f.CloseComponent();
    }));
    b.AddAttribute(13, "Table", (RenderFragment)(t => {
      t.OpenElement(0, "p"); t.AddContent(1, "Тестовая таблица. Длинный текст доступен при фокусе, только если обрезан."); t.CloseElement();
      t.OpenElement(2, "span"); t.AddAttribute(3, "class", "guard-cell-text"); t.AddAttribute(4, "tabindex", "0");
      t.AddContent(5, string.Concat(Enumerable.Repeat("Проверка полного значения и русских букв Ёё. ", 50))); t.CloseElement();
      t.OpenElement(6, "span"); t.AddAttribute(7, "class", "guard-cell-text"); t.AddAttribute(8, "tabindex", "0"); t.AddContent(9, "Короткое значение"); t.CloseElement();
    }));
    b.CloseComponent(); b.CloseElement(); b.CloseElement();
  }

  public sealed class PreviewFields
  {
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
  }
}

public sealed class UiStandardLongDialog : ComponentBase
{
  [Inject] public DialogService Dialog { get; set; } = default!;
  protected override void BuildRenderTree(RenderTreeBuilder b)
  {
    b.OpenComponent<ViewEntity<string>>(0);
    b.AddAttribute(1, "Name", "ПРОВЕРКА ДЛИННОГО ДИАЛОГА");
    b.AddAttribute(2, "OnCancelButtonClick", EventCallback.Factory.Create(this, () => Dialog.Close()));
    b.AddAttribute(3, "ChildContent", (RenderFragment)(c => {
      for (var n = 0; n < 80; n++) {
        c.OpenElement(0, "p"); c.AddContent(1, $"Синтетическое содержимое формы, строка {n}. Ёжик и русские буквы."); c.CloseElement();
      }
    }));
    b.CloseComponent();
  }
}

public sealed class UiStandardAccessDeniedDialog : ComponentBase
{
  [Inject] public DialogService Dialog { get; set; } = default!;
  protected override void BuildRenderTree(RenderTreeBuilder b)
  {
    b.OpenComponent<ViewEntity<object>>(0);
    b.AddAttribute(1, "Name", "ПРОВЕРКА СООБЩЕНИЯ О ДОСТУПЕ");
    b.AddAttribute(2, "OnCancelButtonClick", EventCallback.Factory.Create(this, () => Dialog.Close()));
    b.AddAttribute(3, "ChildContent", (RenderFragment)(c => {
      c.OpenComponent<Guard.Components.Library.Authorization.AccessDenied>(0); c.CloseComponent();
    }));
    b.CloseComponent();
  }
}
