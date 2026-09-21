using Guard.Core.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Guard.Core.Services;

public class ReadContextService : IReadContextService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly IDbContextFactory<ApplicationDbContext> _factory;

  public ReadContextService(
      IServiceProvider serviceProvider,
      IDbContextFactory<ApplicationDbContext> factory)
  {
    _serviceProvider = serviceProvider;
    _factory = factory;
  }

  // Автоматически получает или создает репозиторий для ЛЮБОЙ сущности
  public IReadRepository<T> GetRepository<T>() where T : class
      => _serviceProvider.GetRequiredService<IReadRepository<T>>();
}