using Guard.Core.Contexts;
using Guard.Core.Entities;
using Guard.Core.Repositories;

namespace Core.Repositories
{
  public class SubdivisionRepository : GenericRepository<Subdivision>, ISubdivisionRepository
  {
    public SubdivisionRepository(ApplicationDbContext appDbContext) : base(appDbContext)
    {
    }
  }
}
