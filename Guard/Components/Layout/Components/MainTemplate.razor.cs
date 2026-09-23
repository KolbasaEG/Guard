using Guard.Core.Entities;
using Guard.Core.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.JSInterop;
using Radzen;

namespace Guard.Components.Layout.Components
{
  public partial class MainTemplate
  {
    [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
    [Inject] protected ICurrentUserService CurrentUserService { get; set; } = default!;

    [Parameter] public RenderFragment ChildContent { get; set; } = default!;
    [Parameter] public RenderFragment MenueContent { get; set; } = default!;
    [Parameter] public string Name { get; set; } = string.Empty;
    [Parameter] public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Контекст авторизованного пользователя для каскадной передачи вниз
    /// </summary>
    protected UserContext? UserContext { get; private set; }

    bool leftSidebarExpanded = true;
    bool rightSidebarExpanded = true;

    protected override async Task OnInitializedAsync()
    {
      UserContext = await CurrentUserService.GetContextAsync();
    }

    /// <summary>
    /// Обработчик кнопки выхода (power icon в header).
    /// </summary>
    private void Logout()
    {
      var currentRelativeUrl = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);

      if (string.IsNullOrWhiteSpace(currentRelativeUrl) ||
          currentRelativeUrl.StartsWith("Account/Logout", StringComparison.OrdinalIgnoreCase) ||
          currentRelativeUrl.StartsWith("Account/Login", StringComparison.OrdinalIgnoreCase))
      {
        currentRelativeUrl = "/";
      }

      if (!Uri.IsWellFormedUriString(currentRelativeUrl, UriKind.Relative))
      {
        currentRelativeUrl = "/";
      }

      var logoutUrl = $"/Account/Logout?ReturnUrl={Uri.EscapeDataString(currentRelativeUrl)}";

      NavigationManager.NavigateTo(logoutUrl, forceLoad: true);
    }
  }
}