using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Guard.Core.Contexts;

// Генерация миграций не запускает приложение, seeding и подключения к рабочим БД.
public sealed class ApplicationDbContextDesignFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
  public ApplicationDbContext CreateDbContext(string[] args) => new(
    new DbContextOptionsBuilder<ApplicationDbContext>()
      .UseNpgsql("Host=localhost;Database=guard_design")
      .Options);
}
