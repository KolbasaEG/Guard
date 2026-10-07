using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Guard.Components.Layout.Components;

// Потомки получают новые cascading values напрямую. Повторная передача RenderFragment
// маршрутизатору запускала AuthorizeRouteView заново и уничтожала открытые диалоги.
public sealed class StableAccessContent : ComponentBase
{
  [Parameter] public RenderFragment? ChildContent { get; set; }
  protected override bool ShouldRender() => false;
  protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent);
}
