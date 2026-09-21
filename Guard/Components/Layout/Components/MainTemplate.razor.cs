using Guard.Core.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using System.Security.Claims;


namespace Guard.Components.Layout.Components
{
  public partial class MainTemplate
  {
    [Inject]
    protected IJSRuntime JSRuntime { get; set; }
    [Inject]
    protected NavigationManager NavigationManager { get; set; }
    [Inject]
    protected DialogService DialogService { get; set; }
    [Inject]
    protected TooltipService TooltipService { get; set; }
    [Inject]
    protected ContextMenuService ContextMenuService { get; set; }
    [Inject]
    protected CookieThemeService CookieThemeService { get; set; }
    [Inject]
    protected NotificationService NotificationService { get; set; }
    [Inject]
    protected SignInManager<ApplicationUser> SignInManager { get; set; } = default!;
    [Parameter] public RenderFragment ChildContent { get; set; }
    [Parameter] public RenderFragment MenueContent { get; set; }
    [Parameter] public string Name { get; set; }
    [Parameter] public string Icon { get; set; }
    [CascadingParameter]
    protected Task<AuthenticationState> AuthenticationStateTask { get; set; } = default!;
    protected ClaimsPrincipal? User { get; set; }
    protected override async Task OnInitializedAsync()
    {
      var authState = await AuthenticationStateTask;
      User = authState.User;
    }
    bool leftSidebarExpanded = true;
    bool rightSidebarExpanded = true;
    /// <summary>
    /// Обработчик кнопки выхода (power icon в header).
    /// Перенаправляет на страницу подтверждения выхода с сохранением текущего ReturnUrl.
    /// forceLoad: true — гарантирует переход из InteractiveServer в Static SSR (/Account/*)
    /// и корректную установку/удаление cookies Identity.
    /// </summary>
    private void Logout()
    {
      var currentRelativeUrl = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);

      // Защита: если текущий URL — сам logout или login, отправляем на главную после выхода
      if (string.IsNullOrWhiteSpace(currentRelativeUrl) ||
          currentRelativeUrl.StartsWith("Account/Logout", StringComparison.OrdinalIgnoreCase) ||
          currentRelativeUrl.StartsWith("Account/Login", StringComparison.OrdinalIgnoreCase))
      {
        currentRelativeUrl = "/";
      }

      // Дополнительная защита от Open Redirect (хотя NavigateTo относительный безопасен)
      if (!Uri.IsWellFormedUriString(currentRelativeUrl, UriKind.Relative))
      {
        currentRelativeUrl = "/";
      }

      var logoutUrl = $"/Account/Logout?ReturnUrl={Uri.EscapeDataString(currentRelativeUrl)}";

      // forceLoad: true критично для смены render mode и надёжного SignOut
      NavigationManager.NavigateTo(logoutUrl, forceLoad: true);
    }
  }
}