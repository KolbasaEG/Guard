using Guard.Components.Layout.Navigation;
using Guard.Core.Entities;
using Guard.Core.Identity;
using Guard.Core.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Guard.Components.Layout;

public partial class MainLayout : IDisposable
{
  [Inject] protected ICurrentUserService CurrentUserService { get; set; } = default!;
  [Inject] protected NavigationManager Navigation { get; set; } = default!;
  protected UserContext? UserContext { get; private set; }
  protected string CurrentTitle => NavigationCatalog.Find(Navigation.ToBaseRelativePath(Navigation.Uri))?.Text ?? "АИС Охрана";
  private bool sidebarExpanded = true;
  private bool disposed;

  protected override async Task OnInitializedAsync()
  {
    Navigation.LocationChanged += LocationChanged;
    UserContext = await CurrentUserService.GetContextAsync();
  }

  private void ToggleSidebar() => sidebarExpanded = !sidebarExpanded;
  private void LocationChanged(object? sender, LocationChangedEventArgs args)
  {
    _ = InvokeAsync(() => { if (!disposed) StateHasChanged(); });
  }

  public void Dispose()
  {
    disposed = true;
    Navigation.LocationChanged -= LocationChanged;
  }
}
