using Guard.Core.Entities;
using Guard.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations;

/// <summary>
/// Базовая generic-конфигурация для ВСЕХ сущностей, наследующих <see cref="BaseEntity"/>.
/// </summary>
/// <typeparam name="TEntity">Любая сущность, наследующая BaseEntity</typeparam>
public abstract class BaseEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : BaseEntity
{
  /// <summary>
  /// Основная конфигурация. Вызывается EF Core при построении модели.
  /// </summary>
  public virtual void Configure(EntityTypeBuilder<TEntity> builder)
  {
    // ============================================================
    //                    PRIMARY KEY
    // ============================================================
    // Используем Guid.CreateVersion7() — time-sortable идентификаторы.
    // ValueGeneratedNever() потому что мы генерируем ID в коде (в конструкторе BaseEntity).
    builder.HasKey(e => e.Id);

    builder.Property(e => e.Id)
        .ValueGeneratedNever()
        .IsRequired()
        .HasComment("Уникальный идентификатор записи (UUIDv7)");

    // ============================================================
    //                    STATUS (enum)
    // ============================================================
    // Храним enum как int (1=Inserted, 2=Modified, ..., 6=Deleted).
    // Это позволяет легко фильтровать по статусу и добавлять новые статусы без миграций.
    builder.Property(e => e.Status)
        .HasConversion<int>()
        .IsRequired()
        .HasComment("Текущий статус жизненного цикла записи.");


    // ============================================================
    //                    AUDIT FIELDS (Created / Modified)
    // ============================================================
    builder.Property(e => e.InsertedDate)
        .IsRequired()
        .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'")
        .HasComment("Дата и время создания записи (UTC)");

    builder.Property(e => e.LastModifiedDate)
        .IsRequired(false)
        .HasComment("Дата и время последнего изменения записи (UTC)");

    // === AUDIT FIELDS (CreatedBy / ModifiedBy) ===
    builder.Property(e => e.CreatedBy)
        .IsRequired()
        .HasMaxLength(450)
        .HasComment("Идентификатор пользователя (string), создавшего запись. Ссылается на AspNetUsers.Id");
    builder.Property(e => e.ModifiedBy)
        .IsRequired(false)
        .HasMaxLength(450)
        .HasComment("Идентификатор пользователя (string), последним изменившего запись");


    // ============================================================
    //                    TABLE COMMENT (документация в БД)
    // ============================================================
    builder.ToTable(t =>
    {
      t.HasComment($"Базовая таблица сущности {typeof(TEntity).Name}. " +
                   "Содержит общие поля аудита и статуса жизненного цикла.");
    });
  }
}
