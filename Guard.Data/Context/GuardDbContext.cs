using Guard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Guard.Data.Context
{
  /// <summary>
  /// Контекст базы данных для приложения Guard.
  /// </summary>
  public class GuardDbContext : DbContext
  {
    /// <summary>
    /// Конструктор контекста базы данных.
    /// </summary>
    /// <param name="options">Опции для конфигурации контекста.</param>
    public GuardDbContext(DbContextOptions<GuardDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// DbSet для сущности Subdivision.
    /// </summary>
    public DbSet<Subdivision> Subdivisions { get; set; }

    /// <summary>
    /// Метод для конфигурации модели базы данных.
    /// </summary>
    /// <param name="modelBuilder">Построитель модели базы данных.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
      base.OnModelCreating(modelBuilder);

      // Настройка таблицы для Subdivision
      modelBuilder.Entity<Subdivision>(entity =>
      {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Корневой).HasMaxLength(255);
        entity.Property(e => e.Региональный).HasMaxLength(255);
        entity.Property(e => e.Территориальный).HasMaxLength(255);
        entity.Property(e => e.Субтерриториальный).HasMaxLength(255);
        entity.Property(e => e.Адрес).HasMaxLength(255);
        entity.Property(e => e.Наименование).HasMaxLength(255);
      });

      // Настройка других таблиц базы данных 
      // ...
    }
  }
}