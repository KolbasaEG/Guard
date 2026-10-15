using Microsoft.AspNetCore.Components;
using Radzen;

namespace Guard.Components.Pages;

public partial class Home : IDisposable
{
  [Inject] protected ThemeService ThemeService { get; set; } = default!;
  protected bool IsDarkTheme => ThemeService.Theme?.Contains("-dark", StringComparison.OrdinalIgnoreCase) == true;
  private bool disposed;

  protected override void OnInitialized() => ThemeService.ThemeChanged += ThemeChanged;

  private void ThemeChanged()
  {
    _ = InvokeAsync(() => { if (!disposed) StateHasChanged(); });
  }

  public void Dispose()
  {
    disposed = true;
    ThemeService.ThemeChanged -= ThemeChanged;
  }
}
