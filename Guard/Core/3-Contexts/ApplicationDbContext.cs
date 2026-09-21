using Guard.Core.Entities;
using Guard.Core.Interceptors;
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
  public DbSet<UserIpAddress> UserIpAddresses { get; set; } = null!;
  public DbSet<Subdivision> Subdivisions { get; set; } = null!;
  public DbSet<Classifier> Classifiers { get; set; } = null!;
  public DbSet<Personal> Personals { get; set; } = null!;
}