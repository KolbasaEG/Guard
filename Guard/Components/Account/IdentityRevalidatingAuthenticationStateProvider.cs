using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Guard.Components.Account;

public class IdentityRevalidatingAuthenticationStateProvider<TUser>
    : RevalidatingServerAuthenticationStateProvider
    where TUser : class
{
  private readonly IServiceScopeFactory _scopeFactory;
  private readonly IOptions<IdentityOptions> _options;

  public IdentityRevalidatingAuthenticationStateProvider(
      ILoggerFactory loggerFactory,
      IServiceScopeFactory scopeFactory,
      IOptions<IdentityOptions> options)
      : base(loggerFactory)
  {
    _scopeFactory = scopeFactory;
    _options = options;
  }

  protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

  protected override async Task<bool> ValidateAuthenticationStateAsync(
      AuthenticationState authenticationState, CancellationToken cancellationToken)
  {
    await using var scope = _scopeFactory.CreateAsyncScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();
    var user = await userManager.GetUserAsync(authenticationState.User);

    return user is not null && !await userManager.IsLockedOutAsync(user);
  }
}