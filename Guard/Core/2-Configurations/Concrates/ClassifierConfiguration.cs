using Guard.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Guard.Core.Configurations.Concrates;

public class ClassifierConfiguration : IEntityTypeConfiguration<Classifier>
{
  public void Configure(EntityTypeBuilder<Classifier> builder)
  {
    builder.ToTable("Classifiers", t => t.HasComment("Универсальный справочник классификаторов системы"));

    builder.HasKey(c => c.Id);

    builder.Property(c => c.Id)
        .ValueGeneratedOnAdd()
        .HasComment("Уникальный идентификатор записи классификатора");

    builder.Property(c => c.Type)
        .IsRequired()
        .HasComment("Тип классификатора (числовой код)");

    builder.Property(c => c.Code)
        .IsRequired()
        .HasComment("Код значения внутри конкретного классификатора");

    builder.Property(c => c.ClassifierName)
        .HasMaxLength(256)
        .IsRequired()
        .HasComment("Наименование группы классификатора");

    builder.Property(c => c.Value)
        .HasMaxLength(500)
        .IsRequired()
        .HasComment("Отображаемое значение элемента");

    builder.Property(c => c.IsActive)
        .HasDefaultValue(true)
        .HasComment("Флаг актуальности записи");

    builder.Property(c => c.UpdatedAt)
        .HasColumnType("timestamp with time zone")
        .HasDefaultValueSql("CURRENT_TIMESTAMP")
        .HasComment("Дата и время последнего обновления записи (UTC)");

    // Составной уникальный индекс (Type, Code) — целевой для всех FK
    builder.HasIndex(c => new { c.Type, c.Code })
        .IsUnique();

    // Обычный индекс по типу для быстрых выборок
    builder.HasIndex(c => c.Type);
  }
}