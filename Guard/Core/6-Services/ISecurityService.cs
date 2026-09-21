using Guard.Core.Entities;

namespace Guard.Core.Services
{
  public interface ISecurityService
  {
    Task<ApplicationUser?> GetUserByIdAsync(string id);
  }
}
