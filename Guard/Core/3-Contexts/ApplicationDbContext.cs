using Guard.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Guard.Core.Contexts;

/// <summary>
/// Основной DbContext приложения Guard.
/// 
/// Наследуется от IdentityDbContext с ключом string (стандарт ASP.NET Core Identity).
/// 
/// Особенности:
/// - Применяет все конфигурации из сборки через ApplyConfigurationsFromAssembly.
/// - Содержит DbSet для Subdivision (и будет добавляться по мере развития).
/// - Готов к добавлению глобальных query filters (например, soft delete по Status).
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
  // Стандартный конструктор EF Core без зависимости от Scoped-интерцептора
  public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
      : base(options)
  {
  }

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);
    builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
  }

  public DbSet<IpAddress> IpAddresses { get; set; } = null!;
  public DbSet<Subdivision> Subdivisions { get; set; } = null!;
  public DbSet<Classifier> Classifiers { get; set; } = null!;
  public DbSet<Personal> Personals { get; set; } = null!;
  public DbSet<OrganType> OrganTypes { get; set; } = null!;

  public override int SaveChanges(bool acceptAllChangesOnSuccess)
  {
    PrepareVersions();
    return base.SaveChanges(acceptAllChangesOnSuccess);
  }

  public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
  {
    PrepareVersions();
    return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
  }

  private void PrepareVersions()
  {
    foreach (var entry in ChangeTracker.Entries().Where(e =>
        e.State is EntityState.Added or EntityState.Modified &&
        e.Entity is Personal or Subdivision or Classifier or OrganType))
      entry.Property("Version").CurrentValue = Guid.NewGuid();
  }
}
