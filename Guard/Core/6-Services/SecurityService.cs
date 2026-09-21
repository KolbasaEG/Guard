using Guard.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Guard.Core.Services
{
  public class SecurityService : ISecurityService
  {
    private readonly UserManager<ApplicationUser> userManager;
    public SecurityService(UserManager<ApplicationUser> userManager)
    {
      this.userManager = userManager;
    }

    public async Task<ApplicationUser?> GetUserByIdAsync(string id)
    {
      return await userManager.FindByIdAsync(id);
    }
  }
}
